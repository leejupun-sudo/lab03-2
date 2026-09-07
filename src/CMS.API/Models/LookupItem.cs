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
