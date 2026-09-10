using CMS.API.Models;

namespace CMS.API.Repositories;

public interface IAuthRepository
{
    /// <summary>
    /// 依帳號讀取登入驗證所需的資料列 (含 <c>PasswordHash</c> 與 <c>AppUserRole</c> 角色).
    /// 找不到帳號時回傳 <c>null</c>.
    /// <para>
    /// 這是唯一會 SELECT <c>PasswordHash</c> 的查詢; 呼叫端必須只用於比對, 絕不回傳.
    /// </para>
    /// </summary>
    Task<AppUserCredential?> GetCredentialAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 帳號是否存在<b>且</b>啟用 — 每個帶 token 的請求都會問一次.
    /// <para>
    /// 登入當下的 <c>IsActive</c> 檢查只擋得住「停用後才登入」的人; access token 一旦簽出去就有
    /// 24 小時效期, 中途把帳號停用或刪除並不會讓它失效. 這個查詢就是那個缺口的補丁, 由
    /// <c>ActiveAccountEvents</c> 在 bearer middleware 驗完簽章之後呼叫.
    /// </para>
    /// <para>
    /// 「查無此帳號」與「帳號已停用」一律回 <c>false</c>: 兩者對呼叫端是同一件事 (這張 token 不該
    /// 再被接受), 而且合併之後就不必把帳號存在與否的資訊往外送.
    /// </para>
    /// </summary>
    Task<bool> IsActiveAccountAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新指定帳號的 <c>UserName</c>, 回傳是否有資料列被更新 (帳號不存在時為 <c>false</c>).
    /// <para>
    /// 這是本 repository 唯一的寫入. 它只寫 <c>UserName</c> 一欄 — <c>UserId</c> 是叢集主鍵兼
    /// <c>AppUserRole</c> 外鍵目標、<c>IsActive</c> 與 <c>PasswordHash</c> 屬於管理者維護的範圍,
    /// 角色則完全不在這張表上, 所以「使用者只能改自己的姓名」這條規則在 SQL 這層就成立.
    /// </para>
    /// </summary>
    Task<bool> UpdateUserNameAsync(string userId, string userName, CancellationToken cancellationToken = default);
}
