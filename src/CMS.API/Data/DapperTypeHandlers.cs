using System.Data;
using Dapper;

namespace CMS.API.Data;

/// <summary>Maps <see cref="DateOnly"/> to SQL Server <c>date</c>.</summary>
public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override DateOnly Parse(object value) => DateOnly.FromDateTime((DateTime)value);

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }
}

/// <summary>Maps <see cref="TimeOnly"/> to SQL Server <c>time</c>.</summary>
public class TimeOnlyTypeHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public override TimeOnly Parse(object value) => value switch
    {
        TimeSpan span => TimeOnly.FromTimeSpan(span),
        DateTime dateTime => TimeOnly.FromDateTime(dateTime),
        _ => TimeOnly.Parse((string)value)
    };

    public override void SetValue(IDbDataParameter parameter, TimeOnly value)
    {
        parameter.DbType = DbType.Time;
        parameter.Value = value.ToTimeSpan();
    }
}

public static class DapperTypeHandlers
{
    public static void Register()
    {
        SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
    }
}
