namespace CMS.API.Models;

/// <summary>下拉選單用的精簡項目.</summary>
public class LookupItem<TValue>
{
    public TValue Value { get; set; } = default!;

    public string Label { get; set; } = string.Empty;
}

/// <summary>使用者下拉選單項目 — value 為 UserId.</summary>
public class AppUserLookup
{
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>顯示標籤, 例: <c>Miles Sun (miles@uuu.com.tw)</c>.</summary>
    public string Label => $"{UserName} ({UserId})";
}

/// <summary>發布狀態下拉選單項目 — value 為 pkid.</summary>
public class PublishStatusLookup
{
    public byte Pkid { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>顯示標籤 — 即狀態名稱, 例: <c>上架中</c>.</summary>
    public string Label => Description;
}

/// <summary>課程群組下拉選單項目 — value 為 pkid.</summary>
public class CourseGroupLookup
{
    public short Pkid { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>顯示標籤 — 即群組名稱, 例: <c>SharePoint系列課程</c>.</summary>
    public string Label => Description;
}

/// <summary>合作廠商下拉選單項目 — value 為 pkid.</summary>
public class PartnerLookup
{
    public short Pkid { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AppKey { get; set; } = string.Empty;

    /// <summary>
    /// 顯示標籤, 例: <c>國際標準課程 (ISO)</c>.
    /// <para>
    /// 不能只用 Name — 線上有三筆同名的「國際標準課程」, 只顯示名稱會出現無法分辨的重複選項.
    /// AppKey 是唯一相異的欄位, 作法與 <see cref="AppUserLookup.Label"/> 一致.
    /// </para>
    /// </summary>
    public string Label => $"{Name} ({AppKey})";
}
