using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viora.Application.Statutory;
using Viora.Domain.Entities;
using Viora.Infrastructure.Persistence;
using Viora.Infrastructure.Persistence.Repositories;
using Viora.Infrastructure.Security;

await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=127.0.0.1;Port=55439;Database=statutory_check2;Username=statutory_test").Options);
var repo = new StatutoryRepository(db); var adminId = Guid.NewGuid();
foreach (var role in new[] { AccountRole.Admin, AccountRole.User })
{
    var email = role == AccountRole.Admin ? "admin@statutory.test" : "user@statutory.test";
    if (await db.Accounts.AnyAsync(x => x.Email == email)) continue;
    var account = new Account { Id=Guid.NewGuid(), Email=email, Role=role, PasswordHash=new AspNetIdentityPasswordHasher().Hash("Statutory-Test-2026!"), Status=AccountStatus.Active };
    account.User = new User { Id=Guid.NewGuid(), AccountId=account.Id, DisplayName=role == AccountRole.Admin ? "Quản trị kiểm thử" : "Người dùng kiểm thử" };
    db.Add(account);
}
await db.SaveChangesAsync();
var authority = await repo.AddCatalogAsync(true,"Cơ quan kiểm thử giao diện",default);
var field = await repo.AddCatalogAsync(false,"Lĩnh vực kiểm thử giao diện",default);
var d = await repo.SaveAsync(null,adminId,new("Văn bản kiểm thử giao diện tra cứu pháp luật có tên dài để kiểm tra khả năng xuống dòng trên điện thoại", "UI-TEST/2026",StatutoryType.Code,authority.Id,new(2026,1,1),new(2026,1,2),null,false,"Dữ liệu kiểm thử độc lập trên localhost; kiểm tra tìm kiếm, mục lục và trình đọc nhiều điều khoản.","https://example.org/legal", "https://example.org/legal.pdf",new[]{field.Id}),default);
var chapter = await repo.SaveSectionAsync(d.Id,null,adminId,new(null,LegalNodeType.Chapter,"I","Quy định chung",0,""),default);
for (var i=1;i<=105;i++)
{
    var article=await repo.SaveSectionAsync(d.Id,null,adminId,new(chapter.Id,LegalNodeType.Article,i.ToString(),$"Nội dung kiểm thử điều {i}",i,$"Điều {i} chứa nội dung kiểm thử có cấu trúc.\nĐoạn thứ hai kiểm tra khoảng cách dòng, khả năng đọc và cuộn chính xác đến điều khoản."),default);
    if(i<=3) await repo.SaveSectionAsync(d.Id,null,adminId,new(article.Id,LegalNodeType.Clause,"1","Nội dung khoản",0,"Khoản kiểm thử này thuộc Điều tương ứng."),default);
}
await repo.PublishAsync(d.Id,adminId,true,default);
var draft=await repo.SaveAsync(null,adminId,new("Bản nháp kiểm thử không xuất hiện công khai","UI-DRAFT/2026",StatutoryType.Decree,authority.Id,new(2026,1,1),new(2026,1,2),null,false,null,null,null,new[]{field.Id}),default);
await File.WriteAllTextAsync("../.codex/tmp/statutory-fixture.json",JsonSerializer.Serialize(new { documentId=d.Id, draftId=draft.Id, chapterId=chapter.Id }));
Console.WriteLine("Created isolated admin/user accounts, draft and a published document with 105 articles for browser verification.");
