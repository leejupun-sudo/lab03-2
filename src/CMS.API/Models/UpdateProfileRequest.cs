using System.ComponentModel.DataAnnotations;

namespace CMS.API.Models;

/// <summary>
/// 修改個人資料請求 — <c>PUT /api/auth/profile</c>.
/// <para>
/// 刻意只有 <see cref="UserName"/> 一個屬性. 帳號 (<c>UserId</c>) 取自 JWT, 角色由管理者維護;
/// 兩者都不在此請求中, 所以呼叫端連「送錯欄位」的機會都沒有 — 多送的 JSON 屬性會被忽略.
/// </para>
/// </summary>
public class UpdateProfileRequest
{
    /// <summary>
    /// 姓名 — 必填, 儲存前 trim.
    /// <para>
    /// <see cref="RequiredAttribute"/> 在判斷空值前會先 trim 字串, 所以全空白也是 <c>400</c>.
    /// (Angular 的 <c>Validators.required</c> 不會, 因此表單另外掛 <c>pattern(/\S/)</c>.)
    /// </para>
    /// </summary>
    [Required(ErrorMessage = "姓名為必填")]
    [MaxLength(200)]
    public string UserName { get; set; } = string.Empty;
}
