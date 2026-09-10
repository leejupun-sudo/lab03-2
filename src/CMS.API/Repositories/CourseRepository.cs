using System.Data;
using CMS.API.Data;
using CMS.API.Models;
using Dapper;

namespace CMS.API.Repositories;

public class CourseRepository : ICourseRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CourseRepository(IDbConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    // The three FK labels are JOINed onto the row as flat properties (PartnerName,
    // CourseGroupDescription, PublishStatusDescription) rather than multi-mapped nav objects:
    // one round trip, no splitOn, and the list renders without any lookup calls.
    // CourseGroup is a LEFT JOIN — the column is nullable (even though 0 live rows are null).
    //
    // The four counts drive the detail page and the delete guard. CourseInCertification and
    // CourseJobCategories are NOT counted: both cascade on delete, so they never block.
    // CourseRecomm has no FK at all — it references Course by CourseId *value*, in either
    // column — and is counted for the same reason Seminar is counted on Partner.
    private const string SelectColumns = @"
SELECT c.pkid AS Pkid, c.Title, c.OfficialTitle, c.CourseId, c.ProdCourseId, c.FriendlyUrl,
       c.DisplayOrder,
       c.Partner_pkid       AS PartnerPkid,       p.Name        AS PartnerName,
       c.CourseGroup_pkid   AS CourseGroupPkid,   g.Description AS CourseGroupDescription,
       c.PublishStatus_pkid AS PublishStatusPkid, s.Description AS PublishStatusDescription,
       c.ScheduleOn, c.ScheduleOff, c.[Hour], c.ListPrice, c.LearningCredit,
       c.Material, c.Objective, c.Target, c.Prerequisites, c.Outline, c.TowardCertOrExam,
       c.Note, c.OtherInfo, c.CanRepeat,
       (SELECT COUNT(*) FROM CourseFAQ         f WHERE f.Course_pkid = c.pkid) AS FaqCount,
       (SELECT COUNT(*) FROM CourseRelatedLink l WHERE l.Course_pkid = c.pkid) AS RelatedLinkCount,
       (SELECT COUNT(*) FROM HotCourse         h WHERE h.Course_pkid = c.pkid) AS HotCourseCount,
       (SELECT COUNT(*) FROM CourseRecomm      r WHERE r.CourseId = c.CourseId
                                                    OR r.RecommCourseId = c.CourseId) AS RecommCount
FROM Course c
JOIN      Partner       p ON p.pkid = c.Partner_pkid
LEFT JOIN CourseGroup   g ON g.pkid = c.CourseGroup_pkid
JOIN      PublishStatus s ON s.pkid = c.PublishStatus_pkid";

    // CourseId is unique (IX_Course_UniqueCourseId), so a single key gives a total order.
    // DisplayOrder was rejected: 65 distinct values over 1084 rows — a per-partner ordinal.
    private const string OrderBy = " ORDER BY c.CourseId ASC";

    // Shared by INSERT and COPY so the two column lists cannot drift apart.
    private const string InsertColumns = @"
(Title, OfficialTitle, CourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
 Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, [Hour],
 ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
 TowardCertOrExam, Note, OtherInfo, CanRepeat)";

    public async Task<IEnumerable<Course>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Course>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IEnumerable<Course>> QueryAsync(CourseQuery query, CancellationToken cancellationToken = default)
    {
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            // Short identifying columns only — the eight description blocks are excluded.
            where.Add(@"(c.Title LIKE @Keyword
                      OR c.OfficialTitle LIKE @Keyword
                      OR c.CourseId LIKE @Keyword
                      OR c.ProdCourseId LIKE @Keyword
                      OR c.FriendlyUrl LIKE @Keyword)");
            parameters.Add("Keyword", "%" + query.Keyword.Trim() + "%");
        }

        if (query.PartnerPkid is not null)
        {
            where.Add("c.Partner_pkid = @PartnerPkid");
            parameters.Add("PartnerPkid", query.PartnerPkid);
        }

        if (query.CourseGroupPkid is not null)
        {
            where.Add("c.CourseGroup_pkid = @CourseGroupPkid");
            parameters.Add("CourseGroupPkid", query.CourseGroupPkid);
        }

        if (query.PublishStatusPkid is not null)
        {
            where.Add("c.PublishStatus_pkid = @PublishStatusPkid");
            parameters.Add("PublishStatusPkid", query.PublishStatusPkid);
        }

        if (query.ScheduleOnFrom is not null)
        {
            where.Add("c.ScheduleOn >= @ScheduleOnFrom");
            parameters.Add("ScheduleOnFrom", query.ScheduleOnFrom);
        }

        if (query.ScheduleOnTo is not null)
        {
            where.Add("c.ScheduleOn <= @ScheduleOnTo");
            parameters.Add("ScheduleOnTo", query.ScheduleOnTo);
        }

        if (query.ScheduleOffFrom is not null)
        {
            where.Add("c.ScheduleOff >= @ScheduleOffFrom");
            parameters.Add("ScheduleOffFrom", query.ScheduleOffFrom);
        }

        if (query.ScheduleOffTo is not null)
        {
            where.Add("c.ScheduleOff <= @ScheduleOffTo");
            parameters.Add("ScheduleOffTo", query.ScheduleOffTo);
        }

        if (query.CanRepeat is not null)
        {
            where.Add("c.CanRepeat = @CanRepeat");
            parameters.Add("CanRepeat", query.CanRepeat);
        }

        var sql = SelectColumns
            + (where.Count > 0 ? " WHERE " + string.Join(" AND ", where) : string.Empty)
            + OrderBy;

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QueryAsync<Course>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    public async Task<Course?> GetByIdAsync(int pkid, CancellationToken cancellationToken = default)
    {
        const string sql = SelectColumns + " WHERE c.pkid = @Pkid";
        const string certificationsSql =
            "SELECT Certification_pkid FROM CourseInCertification WHERE Course_pkid = @Pkid ORDER BY Certification_pkid";
        const string jobCategoriesSql =
            "SELECT JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @Pkid ORDER BY JobCategory_pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var course = await connection.QuerySingleOrDefaultAsync<Course>(
            new CommandDefinition(sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
        if (course is null)
        {
            return null;
        }

        // Junction ids ride along only on the single-row read; the list never carries them.
        course.CertificationPkids = (await connection.QueryAsync<int>(
            new CommandDefinition(certificationsSql, new { Pkid = pkid }, cancellationToken: cancellationToken))).ToList();
        course.JobCategoryPkids = (await connection.QueryAsync<short>(
            new CommandDefinition(jobCategoriesSql, new { Pkid = pkid }, cancellationToken: cancellationToken))).ToList();

        return course;
    }

    public async Task<int> CreateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = "INSERT INTO Course " + InsertColumns + @"
VALUES (@Title, @OfficialTitle, @CourseId, @ProdCourseId, @FriendlyUrl, @DisplayOrder,
        @PartnerPkid, @CourseGroupPkid, @PublishStatusPkid, @ScheduleOn, @ScheduleOff, @Hour,
        @ListPrice, @LearningCredit, @Material, @Objective, @Target, @Prerequisites, @Outline,
        @TowardCertOrExam, @Note, @OtherInfo, @CanRepeat);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var pkid = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, WriteParameters(request, includeCourseId: true), transaction, cancellationToken: cancellationToken));

        await SyncJunctionsAsync(connection, transaction, pkid, request, cancellationToken);

        transaction.Commit();
        return pkid;
    }

    public async Task<bool> UpdateAsync(CourseRequest request, CancellationToken cancellationToken = default)
    {
        // pkid is IDENTITY and CourseId is the value CourseRecomm keys on — neither is written.
        const string sql = @"
UPDATE Course
SET Title = @Title, OfficialTitle = @OfficialTitle, ProdCourseId = @ProdCourseId,
    FriendlyUrl = @FriendlyUrl, DisplayOrder = @DisplayOrder,
    Partner_pkid = @PartnerPkid, CourseGroup_pkid = @CourseGroupPkid,
    PublishStatus_pkid = @PublishStatusPkid, ScheduleOn = @ScheduleOn, ScheduleOff = @ScheduleOff,
    [Hour] = @Hour, ListPrice = @ListPrice, LearningCredit = @LearningCredit,
    Material = @Material, Objective = @Objective, Target = @Target, Prerequisites = @Prerequisites,
    Outline = @Outline, TowardCertOrExam = @TowardCertOrExam, Note = @Note, OtherInfo = @OtherInfo,
    CanRepeat = @CanRepeat
WHERE pkid = @Pkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, WriteParameters(request, includeCourseId: false), transaction, cancellationToken: cancellationToken));
        if (affected == 0)
        {
            transaction.Rollback();
            return false;
        }

        await SyncJunctionsAsync(connection, transaction, request.Pkid, request, cancellationToken);

        transaction.Commit();
        return true;
    }

    public async Task<bool> DeleteAsync(int pkid, CancellationToken cancellationToken = default)
    {
        // CourseInCertification / CourseJobCategories cascade; the three NO_ACTION children
        // and CourseRecomm are checked by IsInUseAsync before this runs.
        const string sql = "DELETE FROM Course WHERE pkid = @Pkid";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var affected = await connection.ExecuteAsync(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));

        return affected > 0;
    }

    public async Task<int?> CopyAsync(int sourcePkid, string newCourseId, CancellationToken cancellationToken = default)
    {
        // INSERT ... SELECT keeps every scalar (status, dates, prices, text) verbatim; only
        // CourseId changes. Both junction sets are cloned in the same transaction.
        const string copySql = "INSERT INTO Course " + InsertColumns + @"
SELECT Title, OfficialTitle, @NewCourseId, ProdCourseId, FriendlyUrl, DisplayOrder,
       Partner_pkid, CourseGroup_pkid, PublishStatus_pkid, ScheduleOn, ScheduleOff, [Hour],
       ListPrice, LearningCredit, Material, Objective, Target, Prerequisites, Outline,
       TowardCertOrExam, Note, OtherInfo, CanRepeat
FROM Course WHERE pkid = @SourcePkid;
SELECT CAST(SCOPE_IDENTITY() AS int);";
        const string copyCertificationsSql = @"
INSERT INTO CourseInCertification (Course_pkid, Certification_pkid)
SELECT @NewPkid, Certification_pkid FROM CourseInCertification WHERE Course_pkid = @SourcePkid;";
        const string copyJobCategoriesSql = @"
INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid)
SELECT @NewPkid, JobCategory_pkid FROM CourseJobCategories WHERE Course_pkid = @SourcePkid;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        // SCOPE_IDENTITY() is NULL when the SELECT matched no source row.
        var newPkid = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            copySql, new { NewCourseId = newCourseId, SourcePkid = sourcePkid }, transaction,
            cancellationToken: cancellationToken));
        if (newPkid is null)
        {
            transaction.Rollback();
            return null;
        }

        var junctionArgs = new { NewPkid = newPkid.Value, SourcePkid = sourcePkid };
        await connection.ExecuteAsync(new CommandDefinition(
            copyCertificationsSql, junctionArgs, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            copyJobCategoriesSql, junctionArgs, transaction, cancellationToken: cancellationToken));

        transaction.Commit();
        return newPkid;
    }

    public async Task<bool> CourseIdExistsAsync(string courseId, int? excludePkid = null, CancellationToken cancellationToken = default)
    {
        // The column's collation is Chinese_Taiwan_Stroke_CI_AS, so this equality — like the
        // unique index it fronts — is case-insensitive.
        const string sql = @"
SELECT COUNT(1) FROM Course c
WHERE c.CourseId = @CourseId AND (@ExcludePkid IS NULL OR c.pkid <> @ExcludePkid)";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { CourseId = courseId.Trim(), ExcludePkid = excludePkid }, cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<bool> IsInUseAsync(int pkid, CancellationToken cancellationToken = default)
    {
        // Guards DELETE: the first three would otherwise surface as an FK-violation 500.
        // The CourseRecomm clause is the one SQL Server would NOT enforce — it declares no
        // foreign key and keys on the CourseId string, yet 3118 live rows reference courses
        // through it. Without this clause, deleting such a course silently detaches them.
        const string sql = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM CourseFAQ         WHERE Course_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM CourseRelatedLink WHERE Course_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM HotCourse         WHERE Course_pkid = @Pkid)
              OR EXISTS (SELECT 1 FROM CourseRecomm r
                         JOIN Course c ON c.pkid = @Pkid
                         WHERE r.CourseId = c.CourseId OR r.RecommCourseId = c.CourseId)
            THEN 1 ELSE 0 END;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var inUse = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { Pkid = pkid }, cancellationToken: cancellationToken));
        return inUse == 1;
    }

    /// <summary>Delete-then-reinsert both junctions inside the caller's transaction.</summary>
    private static async Task SyncJunctionsAsync(
        IDbConnection connection,
        IDbTransaction transaction,
        int pkid,
        CourseRequest request,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseInCertification WHERE Course_pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM CourseJobCategories WHERE Course_pkid = @Pkid",
            new { Pkid = pkid }, transaction, cancellationToken: cancellationToken));

        // Distinct() protects the composite primary keys from a repeated id in the request.
        var certifications = request.CertificationPkids.Distinct()
            .Select(certificationPkid => new { Pkid = pkid, CertificationPkid = certificationPkid })
            .ToList();
        if (certifications.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CourseInCertification (Course_pkid, Certification_pkid) VALUES (@Pkid, @CertificationPkid)",
                certifications, transaction, cancellationToken: cancellationToken));
        }

        var jobCategories = request.JobCategoryPkids.Distinct()
            .Select(jobCategoryPkid => new { Pkid = pkid, JobCategoryPkid = jobCategoryPkid })
            .ToList();
        if (jobCategories.Count > 0)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO CourseJobCategories (Course_pkid, JobCategory_pkid) VALUES (@Pkid, @JobCategoryPkid)",
                jobCategories, transaction, cancellationToken: cancellationToken));
        }
    }

    private static DynamicParameters WriteParameters(CourseRequest request, bool includeCourseId)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Pkid", request.Pkid);
        parameters.Add("Title", request.Title.Trim());
        parameters.Add("OfficialTitle", NullIfBlank(request.OfficialTitle));
        if (includeCourseId)
        {
            parameters.Add("CourseId", request.CourseId.Trim());
        }

        parameters.Add("ProdCourseId", request.ProdCourseId.Trim());
        parameters.Add("FriendlyUrl", request.FriendlyUrl.Trim());
        parameters.Add("DisplayOrder", request.DisplayOrder);
        parameters.Add("PartnerPkid", request.PartnerPkid);
        parameters.Add("CourseGroupPkid", request.CourseGroupPkid);
        parameters.Add("PublishStatusPkid", request.PublishStatusPkid);
        // DataAnnotations guarantee both dates are present by the time the repository runs.
        parameters.Add("ScheduleOn", request.ScheduleOn!.Value);
        parameters.Add("ScheduleOff", request.ScheduleOff!.Value);
        parameters.Add("Hour", request.Hour);
        parameters.Add("ListPrice", request.ListPrice);
        parameters.Add("LearningCredit", request.LearningCredit);
        parameters.Add("Material", NullIfBlank(request.Material));
        parameters.Add("Objective", NullIfBlank(request.Objective));
        parameters.Add("Target", NullIfBlank(request.Target));
        parameters.Add("Prerequisites", NullIfBlank(request.Prerequisites));
        parameters.Add("Outline", NullIfBlank(request.Outline));
        parameters.Add("TowardCertOrExam", NullIfBlank(request.TowardCertOrExam));
        parameters.Add("Note", NullIfBlank(request.Note));
        parameters.Add("OtherInfo", NullIfBlank(request.OtherInfo));
        parameters.Add("CanRepeat", request.CanRepeat);
        return parameters;
    }

    /// <summary>The optional text columns hold NULLs, never empty strings — blank is stored as NULL.</summary>
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
