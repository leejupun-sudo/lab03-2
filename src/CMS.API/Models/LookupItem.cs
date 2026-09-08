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

/// <summary>認證下拉選單項目 — value 為 pkid.</summary>
public class CertificationLookup
{
    public int Pkid { get; set; }

    /// <summary>認證名稱 — 來源欄位是 nchar(100), SELECT 時已 RTRIM.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>所屬原廠名稱 — JOIN Partner.Name.</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>
    /// 顯示標籤, 例: <c>FCP-PCS (Fortinet資安專家認證課程)</c>.
    /// 39 個 Title 全部相異, 附上原廠是為了讓 <c>FCP-PCS</c> 這類代碼有脈絡, 不是為了消歧.
    /// </summary>
    public string Label => $"{Title} ({PartnerName})";
}

/// <summary>職務類別下拉選單項目 — value 為 pkid.</summary>
public class JobCategoryLookup
{
    public short Pkid { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>顯示標籤 — 即類別名稱, 例: <c>資訊安全 Security</c>.</summary>
    public string Label => Description;
}

/// <summary>課程下拉選單項目 — value 為 pkid. 線上約 1084 筆, 消費端需要 virtualScroll.</summary>
public class CourseLookup
{
    public int Pkid { get; set; }

    public string CourseId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>顯示標籤, 例: <c>PLF Oracle資料庫之PL／SQL基礎</c>.</summary>
    public string Label => $"{CourseId} {Title}";
}
