using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CMS.API.Models;
using Xunit;

namespace CMS.API.Tests;

public class CoursesControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // ---------- List / filter ----------

    [Fact]
    public async Task GetAll_ReturnsAllCourses_OrderedByCourseId()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/courses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await ReadCoursesAsync(response);
        Assert.Equal(["AZ-104", "AZ-900", "IINS", "PLF"], courses.Select(c => c.CourseId));
    }

    /// <summary>The list carries the JOINed labels so the table renders without lookup calls.</summary>
    [Fact]
    public async Task GetAll_CarriesResolvedForeignKeyLabels()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await ReadCoursesAsync(await client.GetAsync("/api/courses"));

        var plf = courses.Single(c => c.CourseId == "PLF");
        Assert.Equal("Oracle", plf.PartnerName);
        Assert.Equal("Oracle DB/My SQL資料庫系列課程", plf.CourseGroupDescription);
        Assert.Equal("已下架", plf.PublishStatusDescription);

        var noGroup = courses.Single(c => c.CourseId == "AZ-900");
        Assert.Null(noGroup.CourseGroupPkid);
        Assert.Null(noGroup.CourseGroupDescription);
    }

    [Fact]
    public async Task GetAll_DoesNotCarryJunctionIds()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await ReadCoursesAsync(await client.GetAsync("/api/courses"));

        // pkid 1 has two certifications on GetById, but the list omits them.
        Assert.All(courses, c => Assert.Empty(c.CertificationPkids));
        Assert.All(courses, c => Assert.Empty(c.JobCategoryPkids));
    }

    [Fact]
    public async Task GetAll_IncludesAllFourUsageCounts()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await ReadCoursesAsync(await client.GetAsync("/api/courses"));

        var plf = courses.Single(c => c.CourseId == "PLF");
        Assert.Equal(2, plf.FaqCount);
        Assert.Equal(3, plf.RelatedLinkCount);
        Assert.Equal(0, plf.HotCourseCount);
        Assert.Equal(0, plf.RecommCount);

        var recommOnly = courses.Single(c => c.CourseId == "IINS");
        Assert.Equal(5, recommOnly.RecommCount);
        Assert.Equal(0, recommOnly.FaqCount + recommOnly.RelatedLinkCount + recommOnly.HotCourseCount);
    }

    [Theory]
    [InlineData("PL／SQL", "PLF")]       // Title
    [InlineData("iins", "IINS")]         // CourseId, case-insensitive
    [InlineData("IINSP", "IINS")]        // ProdCourseId (fake seeds it as CourseId + "P")
    [InlineData("Fundamentals", "AZ-104", "AZ-900")] // FriendlyUrl, shared by two rows
    public async Task Query_WithKeyword_MatchesIdentifyingColumns(string keyword, params string[] expected)
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { Keyword = keyword });

        Assert.Equal(expected, courses.Select(c => c.CourseId));
    }

    /// <summary>The description blocks are excluded from the keyword search.</summary>
    [Fact]
    public async Task Query_WithKeyword_DoesNotSearchOutline()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { Keyword = "Day 1" });

        Assert.Empty(courses);
    }

    [Fact]
    public async Task Query_ByPartner_ReturnsOnlyThatPartnersCourses()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { PartnerPkid = 1 });

        Assert.Equal(["AZ-104", "AZ-900"], courses.Select(c => c.CourseId));
    }

    [Fact]
    public async Task Query_ByCourseGroup_ReturnsOnlyThatGroup()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { CourseGroupPkid = 18 });

        Assert.Equal("PLF", Assert.Single(courses).CourseId);
    }

    [Fact]
    public async Task Query_ByPublishStatus_ReturnsOnlyThatStatus()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { PublishStatusPkid = 2 });

        Assert.Equal(["AZ-104", "IINS"], courses.Select(c => c.CourseId));
    }

    [Fact]
    public async Task Query_ByScheduleOnRange_IsInclusiveAtBothEnds()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery
        {
            ScheduleOnFrom = new DateOnly(2015, 11, 9),
            ScheduleOnTo = new DateOnly(2015, 11, 10)
        });

        Assert.Equal(["IINS", "PLF"], courses.Select(c => c.CourseId));
    }

    [Fact]
    public async Task Query_ByScheduleOffFrom_ExcludesEarlierRows()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { ScheduleOffFrom = new DateOnly(2021, 11, 1) });

        Assert.Equal(["AZ-104", "AZ-900", "PLF"], courses.Select(c => c.CourseId));
    }

    [Theory]
    [InlineData(true, "IINS")]
    [InlineData(false, "AZ-104", "AZ-900", "PLF")]
    public async Task Query_ByCanRepeat_IsTriState(bool canRepeat, params string[] expected)
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery { CanRepeat = canRepeat });

        Assert.Equal(expected, courses.Select(c => c.CourseId));
    }

    [Fact]
    public async Task Query_WithEmptyFilter_ReturnsAllCourses()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var courses = await QueryAsync(client, new CourseQuery());

        Assert.Equal(4, courses.Count);
    }

    // ---------- View ----------

    [Fact]
    public async Task GetById_ReturnsTheCourseWithEveryFieldAndJunctionIds()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/courses/1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.NotNull(course);
        Assert.Equal(1, course.Pkid);
        Assert.Equal("PLF", course.CourseId);
        Assert.Equal("PLFP", course.ProdCourseId);
        Assert.Equal("Oracle資料庫之PL／SQL基礎", course.Title);
        Assert.Equal((short)2, course.PartnerPkid);
        Assert.Equal((short)18, course.CourseGroupPkid);
        Assert.Equal((byte)3, course.PublishStatusPkid);
        Assert.Equal(new DateOnly(2015, 11, 10), course.ScheduleOn);
        Assert.Equal(new DateOnly(2021, 11, 1), course.ScheduleOff);
        Assert.Equal((short)12, course.Hour);
        Assert.Equal(49000m, course.ListPrice);
        Assert.Equal(14.0m, course.LearningCredit);
        Assert.False(course.CanRepeat);
        // Ordered by pkid, like the SQL.
        Assert.Equal([34, 36], course.CertificationPkids);
        Assert.Equal([(short)19, (short)22], course.JobCategoryPkids);
    }

    /// <summary>date columns must serialise as plain yyyy-MM-dd, not a DateTime with a time part.</summary>
    [Fact]
    public async Task GetById_SerialisesDateOnlyColumnsAsIsoDates()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/courses/1");

        using var document = JsonDocument.Parse(json);
        Assert.Equal("2015-11-10", document.RootElement.GetProperty("scheduleOn").GetString());
        Assert.Equal("2021-11-01", document.RootElement.GetProperty("scheduleOff").GetString());
    }

    [Fact]
    public async Task GetById_UnknownPkid_Returns404()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/courses/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Add ----------

    [Fact]
    public async Task Create_AssignsAnIdentityPkid_SyncsJunctions_AndReturnsCreated()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.CertificationPkids = [40, 41];
        request.JobCategoryPkids = [16, 23];

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(5, created.Pkid);
        Assert.Equal("AI-900", created.CourseId);
        Assert.Equal([40, 41], created.CertificationPkids);
        Assert.Equal([(short)16, (short)23], created.JobCategoryPkids);
        Assert.Equal($"/api/courses/{created.Pkid}", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Create_IgnoresAnyPkidSuppliedInTheBody()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.Pkid = 99;

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        var created = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.Equal(5, created!.Pkid);
    }

    /// <summary>CourseId is backed by a UNIQUE index the DDL never declared — a raw insert would 500.</summary>
    [Theory]
    [InlineData("PLF")]
    [InlineData("plf")]   // the column's collation is case-insensitive, so the index is too
    [InlineData(" PLF ")] // trimmed before the check
    public async Task Create_WithACourseIdThatAlreadyExists_Returns409(string courseId)
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/courses", Request(courseId), JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("簡介代碼重複", problem?.Title);

        Assert.Equal(4, (await ReadCoursesAsync(await client.GetAsync("/api/courses"))).Count);
    }

    /// <summary>Title, ProdCourseId and FriendlyUrl are all non-unique in the live data.</summary>
    [Fact]
    public async Task Create_WithATitleAndFriendlyUrlAnotherRowUses_Returns201NotConflict()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AZ-305", title: "Azure基礎");
        request.FriendlyUrl = "Azure-Fundamentals";
        request.ProdCourseId = "PLFP";

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var courses = await ReadCoursesAsync(await client.GetAsync("/api/courses"));
        Assert.Equal(3, courses.Count(c => c.Title == "Azure基礎"));
    }

    [Fact]
    public async Task Create_WithNullCourseGroup_Succeeds()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.CourseGroupPkid = null;

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.Null(created!.CourseGroupPkid);
    }

    [Fact]
    public async Task Create_WithBlankOptionalText_StoresNull()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.OfficialTitle = "   ";
        request.Outline = "";
        request.Note = null;

        var created = await (await client.PostAsJsonAsync("/api/courses", request, JsonOptions))
            .Content.ReadFromJsonAsync<Course>(JsonOptions);

        Assert.Null(created!.OfficialTitle);
        Assert.Null(created.Outline);
        Assert.Null(created.Note);
    }

    [Fact]
    public async Task Create_WithADuplicateJunctionId_StoresItOnce()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.JobCategoryPkids = [16, 16, 23];

        var created = await (await client.PostAsJsonAsync("/api/courses", request, JsonOptions))
            .Content.ReadFromJsonAsync<Course>(JsonOptions);

        Assert.Equal([(short)16, (short)23], created!.JobCategoryPkids);
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("CourseId")]
    [InlineData("ProdCourseId")]
    [InlineData("FriendlyUrl")]
    public async Task Create_WithABlankRequiredString_Returns400(string field)
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        typeof(CourseRequest).GetProperty(field)!.SetValue(request, string.Empty);

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutPartner_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.PartnerPkid = 0;

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutPublishStatus_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.PublishStatusPkid = 0;

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A missing date must be a 400, not a silent 0001-01-01.</summary>
    [Fact]
    public async Task Create_WithoutScheduleOff_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.ScheduleOff = null;

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>No CHECK constraint and one live row violates it — the API accepts an inverted range.</summary>
    [Fact]
    public async Task Create_WithScheduleOffBeforeScheduleOn_Returns201()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("NINS-1");
        request.ScheduleOn = new DateOnly(2023, 2, 17);
        request.ScheduleOff = new DateOnly(2013, 12, 17);

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithAnOverlongFriendlyUrl_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        // nvarchar(100), and the longest live value is exactly 100 characters.
        var request = Request("AI-900");
        request.FriendlyUrl = new string('x', 101);

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNegativeHour_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AI-900");
        request.Hour = -1;

        var response = await client.PostAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithFractionalLearningCredit_KeepsTheFraction()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        // decimal(9,1) — 387 live rows carry a fraction.
        var request = Request("AI-900");
        request.LearningCredit = 7.5m;

        var created = await (await client.PostAsJsonAsync("/api/courses", request, JsonOptions))
            .Content.ReadFromJsonAsync<Course>(JsonOptions);

        Assert.Equal(7.5m, created!.LearningCredit);
    }

    // ---------- Edit ----------

    [Fact]
    public async Task Update_ChangesEveryWritableField_AndReturnsTheUpdatedRow()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AZ-900", title: "Azure基礎（改版）");
        request.Pkid = 3;
        request.CourseGroupPkid = 3;
        request.PublishStatusPkid = 2;
        request.Hour = 14;
        request.ListPrice = 12000;
        request.CanRepeat = true;
        request.Objective = "了解 Azure";
        request.JobCategoryPkids = [16];

        var response = await client.PutAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(3, updated.Pkid);
        Assert.Equal("Azure基礎（改版）", updated.Title);
        Assert.Equal((short)3, updated.CourseGroupPkid);
        Assert.Equal((byte)2, updated.PublishStatusPkid);
        Assert.Equal((short)14, updated.Hour);
        Assert.Equal(12000m, updated.ListPrice);
        Assert.True(updated.CanRepeat);
        Assert.Equal("了解 Azure", updated.Objective);
        Assert.Equal([(short)16], updated.JobCategoryPkids);
    }

    /// <summary>CourseRecomm keys on the CourseId value — the column is frozen after creation.</summary>
    [Fact]
    public async Task Update_IgnoresAChangedCourseId()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("IINS-RENAMED", title: "CCNA Security認證-建置Cisco網路安全");
        request.Pkid = 2;

        var response = await client.PutAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.Equal("IINS", updated!.CourseId);
    }

    /// <summary>
    /// Because CourseId is not written, sending another row's CourseId on update is harmless
    /// and must not 409 — there is no duplicate check on the update path.
    /// </summary>
    [Fact]
    public async Task Update_SendingAnotherRowsCourseId_Returns200AndKeepsItsOwn()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("PLF", title: "Azure基礎");
        request.Pkid = 3;

        var response = await client.PutAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.Equal("AZ-900", updated!.CourseId);
    }

    [Fact]
    public async Task Update_ReplacesTheJunctionSets()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("PLF", title: "Oracle資料庫之PL／SQL基礎");
        request.Pkid = 1;
        request.CertificationPkids = [36];   // 34 removed
        request.JobCategoryPkids = [];       // both removed

        var updated = await (await client.PutAsJsonAsync("/api/courses", request, JsonOptions))
            .Content.ReadFromJsonAsync<Course>(JsonOptions);

        Assert.Equal([36], updated!.CertificationPkids);
        Assert.Empty(updated.JobCategoryPkids);
    }

    [Fact]
    public async Task Update_WithoutPkid_Returns400AndDoesNotTouchRepository()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AZ-900");
        request.Pkid = 0;

        var response = await client.PutAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Repository.UpdateCallCount);
    }

    [Fact]
    public async Task Update_UnknownPkid_Returns404()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("GHOST");
        request.Pkid = 999;

        var response = await client.PutAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithBlankTitle_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var request = Request("AZ-900", title: string.Empty);
        request.Pkid = 3;

        var response = await client.PutAsJsonAsync("/api/courses", request, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Delete ----------

    [Fact]
    public async Task Delete_UnusedCourse_Returns204_ThenGetReturns404()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/courses/3")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/courses/3")).StatusCode);
        Assert.Equal(3, (await ReadCoursesAsync(await client.GetAsync("/api/courses"))).Count);
    }

    /// <summary>Having certifications / job categories does not block — both junctions cascade.</summary>
    [Fact]
    public async Task Delete_CourseWithOnlyJunctionRows_Returns204()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        // pkid 4 has a job category and nothing else.
        var response = await client.DeleteAsync("/api/courses/4");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_CourseUsedByFaqAndLinks_Returns409AndKeepsTheRow()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/1");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("課程使用中", problem?.Title);
        Assert.Contains("2 筆課程問答", problem?.Detail);
        Assert.Contains("3 筆相關連結", problem?.Detail);

        Assert.Equal(4, (await ReadCoursesAsync(await client.GetAsync("/api/courses"))).Count);
    }

    /// <summary>
    /// The case SQL Server would NOT catch: CourseRecomm declares no foreign key and keys on
    /// the CourseId string, so the database would allow this delete. The guard has to carry it.
    /// </summary>
    [Fact]
    public async Task Delete_CourseReferencedOnlyByCourseRecomm_Returns409AndKeepsTheRow()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/2");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("課程使用中", problem?.Title);
        Assert.Contains("5 筆推薦課程", problem?.Detail);

        Assert.Equal(4, (await ReadCoursesAsync(await client.GetAsync("/api/courses"))).Count);
    }

    [Fact]
    public async Task Delete_UnknownPkid_Returns404()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/courses/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---------- Copy ----------

    [Fact]
    public async Task Copy_ClonesEveryFieldAndBothJunctionSets_UnderTheNewCourseId()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/courses/1/copy", new CourseCopyRequest { NewCourseId = "PLF-2" }, JsonOptions);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var copy = await response.Content.ReadFromJsonAsync<Course>(JsonOptions);
        Assert.NotNull(copy);
        Assert.Equal(5, copy.Pkid);
        Assert.Equal("PLF-2", copy.CourseId);
        Assert.Equal("Oracle資料庫之PL／SQL基礎", copy.Title);
        Assert.Equal((byte)3, copy.PublishStatusPkid);   // status copied verbatim
        Assert.Equal(49000m, copy.ListPrice);
        Assert.Equal([34, 36], copy.CertificationPkids);
        Assert.Equal([(short)19, (short)22], copy.JobCategoryPkids);
        Assert.Equal($"/api/courses/{copy.Pkid}", response.Headers.Location?.AbsolutePath);

        // The source is untouched and the copy starts unreferenced.
        Assert.Equal(0, copy.FaqCount + copy.RelatedLinkCount + copy.HotCourseCount + copy.RecommCount);
        Assert.Equal(5, (await ReadCoursesAsync(await client.GetAsync("/api/courses"))).Count);
    }

    [Fact]
    public async Task Copy_WithACourseIdThatAlreadyExists_Returns409()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/courses/1/copy", new CourseCopyRequest { NewCourseId = "az-900" }, JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>(JsonOptions);
        Assert.Equal("簡介代碼重複", problem?.Title);
        Assert.Equal(4, (await ReadCoursesAsync(await client.GetAsync("/api/courses"))).Count);
    }

    [Fact]
    public async Task Copy_UnknownSource_Returns404()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/courses/999/copy", new CourseCopyRequest { NewCourseId = "NEW" }, JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Copy_WithBlankNewCourseId_Returns400(string newCourseId)
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/courses/1/copy", new CourseCopyRequest { NewCourseId = newCourseId }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Copy_WithAnOverlongNewCourseId_Returns400()
    {
        using var factory = new CourseApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/courses/1/copy", new CourseCopyRequest { NewCourseId = new string('x', 51) }, JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Helpers ----------

    private static CourseRequest Request(string courseId, string title = "新課程") => new()
    {
        Title = title,
        CourseId = courseId,
        ProdCourseId = courseId + "P",
        FriendlyUrl = title,
        DisplayOrder = 0,
        PartnerPkid = 1,
        CourseGroupPkid = 3,
        PublishStatusPkid = 1,
        ScheduleOn = new DateOnly(2026, 1, 16),
        ScheduleOff = new DateOnly(2036, 1, 16),
        Hour = 7,
        ListPrice = 9000,
        LearningCredit = 3
    };

    private static async Task<List<Course>> QueryAsync(HttpClient client, CourseQuery query)
    {
        var content = new StringContent(
            JsonSerializer.Serialize(query, JsonOptions), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/courses/query", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadCoursesAsync(response);
    }

    private static async Task<List<Course>> ReadCoursesAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<List<Course>>(JsonOptions) ?? [];

    private sealed class ProblemDetailsDto
    {
        public string? Title { get; set; }

        public string? Detail { get; set; }
    }
}
