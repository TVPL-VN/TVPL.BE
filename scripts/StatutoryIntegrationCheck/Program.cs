using Microsoft.EntityFrameworkCore;
using Viora.Application.Statutory;
using Viora.Domain.Entities;
using Viora.Infrastructure.Persistence;
using Viora.Infrastructure.Persistence.Repositories;

// Isolated loopback test database only; never reads application connection settings.
var testDatabase = args.FirstOrDefault() ?? "statutory_check";
if (!System.Text.RegularExpressions.Regex.IsMatch(testDatabase, "^statutory_check[0-9]*$")) throw new ArgumentException("Only isolated test database names accepted.");
var connection = $"Host=127.0.0.1;Port=55439;Database={testDatabase};Username=statutory_test;Timeout=5";
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
await db.Database.MigrateAsync();
var repo = new StatutoryRepository(db); var actor = Guid.NewGuid(); var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
async Task Reject(Func<Task> action, int status, string message) { try { await action(); throw new Exception("Expected rejection: " + message); } catch (StatutoryException e) { Check(e.Status == status, message); } }
var authority = await repo.AddCatalogAsync(true, "Cơ quan kiểm thử", default);
var field = await repo.AddCatalogAsync(false, "Dân sự kiểm thử", default);
var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
SaveStatutoryRequest Request(string number, DateOnly from) => new("Văn bản kiểm thử " + number, number, StatutoryType.Law, authority.Id, from.AddDays(-2), from, null, false, "Kiểm thử kho văn bản độc lập", "https://example.org/law", null, new[] { field.Id });
var original = await repo.SaveAsync(null, actor, Request("TEST-ORIGINAL", today.AddDays(-30)), default);
Check((await repo.SearchAsync(new(), false, default)).Total == 0, "draft not searchable publicly");
Check(await repo.DetailAsync(original.Id, false, null, default) == null, "draft detail hidden");
await Reject(() => repo.PublishAsync(original.Id, actor, true, default), 400, "empty publication denied");
var article = await repo.SaveSectionAsync(original.Id, null, actor, new(null, LegalNodeType.Article, "1", "Phạm vi điều chỉnh", 0, "Nội dung gốc quyền dân sự"), default);
var clause = await repo.SaveSectionAsync(original.Id, null, actor, new(article.Id, LegalNodeType.Clause, "1", "Khoản một", 0, "Khoản cũ độc nhất"), default);
await Reject(() => repo.SaveSectionAsync(original.Id, null, actor, new(null, LegalNodeType.Article, "1", "Điều trùng", 1, "Không được lưu"), default), 409, "duplicate citation path rejected");
await Reject(() => repo.SaveSectionAsync(original.Id, null, actor, new(null, LegalNodeType.Article, "1/2", "Đường dẫn mơ hồ", 1, "Không được lưu"), default), 400, "path separator cannot spoof ancestry");
await Reject(() => repo.SaveSectionAsync(original.Id, null, actor, new(article.Id, LegalNodeType.Point, "a", "Điểm sai", 0, "sai"), default), 400, "invalid hierarchy denied");
await repo.PublishAsync(original.Id, actor, true, default);
Check((await repo.SearchAsync(new() { Q="quyền dân sự", FieldId=field.Id, AuthorityId=authority.Id }, false, default)).Total == 1, "server search current content and filters");
Check((await repo.SearchAsync(new() { Type=StatutoryType.Decree }, false, default)).Total == 0, "type filter");
Check((await repo.SearchAsync(new() { Q="Điều 1" }, false, default)).Total == 1, "article reference search");
Check((await repo.SearchAsync(new() { Q="Khoản 1" }, false, default)).Total == 1, "clause reference search");
await Reject(() => repo.SaveSectionAsync(original.Id, article.Id, actor, new(null, LegalNodeType.Article, "1", "Ghi đè", 0, "sai"), default), 409, "published content immutable");
var cause = await repo.SaveAsync(null, actor, Request("TEST-AMENDMENT", today.AddDays(-10)), default);
await repo.SaveSectionAsync(cause.Id, null, actor, new(null, LegalNodeType.Article, "1", "Sửa đổi điều 1", 0, "Sửa đổi văn bản gốc"), default);
await Reject(() => repo.AmendAsync(article.Id, actor, new("Thay đổi", today, cause.Id, LegalChangeType.Amended, null, true), default), 409, "unpublished cause denied");
await repo.PublishAsync(cause.Id, actor, true, default);
var draft = await repo.AmendAsync(article.Id, actor, new("Nội dung thay thế độc nhất", today.AddDays(-1), cause.Id, LegalChangeType.Replaced, "Thay Điều 1", false), default);
Check((await repo.DetailAsync(original.Id, false, null, default))!.Sections.Single(s=>s.Id==article.Id).CurrentVersion!.VersionNumber == 1, "draft amendment hidden");
await repo.PublishVersionAsync(article.Id, draft.Id, actor, default);
var current = (await repo.DetailAsync(original.Id, false, null, default))!;
Check(current.Sections.Single(s=>s.Id==article.Id).CurrentVersion!.Content == "Nội dung thay thế độc nhất", "published amendment applied");
Check(!current.Sections.Any(s=>s.Id==clause.Id), "replaced article suppresses old child clause");
await Reject(() => repo.AmendAsync(clause.Id, actor, new("Khoản sau thay thế", today, cause.Id, LegalChangeType.Amended, null, true), default), 409, "cannot append stale child below full parent replacement");
Check((await repo.SearchAsync(new() { Q="Khoản cũ độc nhất" }, false, default)).Total == 0, "search does not match superseded children");
var old = (await repo.DetailAsync(original.Id, false, today.AddDays(-2), default))!;
Check(old.Sections.Any(s=>s.Id==clause.Id), "historical reader retains child clauses");
var versions = await repo.VersionsAsync(article.Id, false, default);
Check(versions.Count == 2 && versions[0].ValidTo == today.AddDays(-2), "old end day before next start");
var future = await repo.AmendAsync(article.Id, actor, new("Nội dung tương lai", today.AddDays(10), cause.Id, LegalChangeType.Amended, null, true), default);
Check((await repo.DetailAsync(original.Id, false, null, default))!.Sections.Single(s=>s.Id==article.Id).CurrentVersion!.Id == draft.Id, "future amendment does not apply early");
Check((await repo.DetailAsync(original.Id, false, today.AddDays(10), default))!.Sections.Single(s=>s.Id==article.Id).CurrentVersion!.Id == future.Id, "date selects future amendment inclusively");
Check((await repo.SearchAsync(new() { Q="Nội dung tương lai" }, false, default)).Total == 0, "future content not searchable as current");
await Reject(() => repo.AmendAsync(article.Id, actor, new("Ghi đè", today, cause.Id, LegalChangeType.Amended, null, true), default), 409, "retroactive overlapping amendment denied");
await Reject(() => repo.DeleteAsync(original.Id, default), 409, "history prevents deletion");
await repo.PublishAsync(cause.Id, actor, false, default);
Check((await repo.VersionsAsync(article.Id, false, default)).Count == 1, "unpublished source changes hidden");
Check((await repo.DetailAsync(original.Id, false, null, default))!.Sections.Any(s=>s.Id==clause.Id), "original children restore when cause unpublished");
await repo.PublishAsync(cause.Id, actor, true, default);
Check(current.Relations.Any(r=>r.Incoming && r.Type==LegalRelationType.Replaces), "amendment relation attached automatically");
Check((await repo.SearchAsync(new() { PageSize=1, Page=2 }, false, default)).Items.Count == 1, "database pagination");
await Reject(() => repo.SearchAsync(new() { Page=int.MaxValue }, false, default), 400, "offset overflow rejected");
foreach (var status in new[] { StatutoryValidity.Upcoming, StatutoryValidity.PartiallyExpired, StatutoryValidity.Expired })
{
    var start = status == StatutoryValidity.Upcoming ? today.AddDays(5) : today.AddDays(-20);
    var request = Request("TEST-STATUS-" + status, start) with { PartiallyExpired=status==StatutoryValidity.PartiallyExpired, ExpiresOn=status==StatutoryValidity.Expired?today:null };
    var document = await repo.SaveAsync(null,actor,request,default);
    await repo.SaveSectionAsync(document.Id,null,actor,new(null,LegalNodeType.Article,"1","Điều kiểm thử",0,"Nội dung kiểm thử hiệu lực"),default);
    await repo.PublishAsync(document.Id,actor,true,default);
    Check((await repo.SearchAsync(new() { Validity=status },false,default)).Items.Any(d=>d.Id==document.Id), "validity SQL filter " + status);
    if(status == StatutoryValidity.Upcoming)
    {
        var section=(await repo.DetailAsync(document.Id,true,null,default))!.Sections.Single();
        await repo.AmendAsync(section.Id,actor,new("Phiên bản tương lai của văn bản chưa hiệu lực",today.AddDays(10),cause.Id,LegalChangeType.Amended,null,true),default);
        Check((await repo.DetailAsync(document.Id,false,null,default))!.Sections.Single().CurrentVersion!.ValidTo == today.AddDays(9),"upcoming original preview retains next effective boundary");
    }
}
async Task<int> Race()
{
    await using var other = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    try { await new StatutoryRepository(other).AmendAsync(article.Id,actor,new("Phiên bản cạnh tranh",today.AddDays(20),cause.Id,LegalChangeType.Amended,null,true),default);return 200; }
    catch (StatutoryException e) { return e.Status; }
}
var concurrent = await Task.WhenAll(Race(),Race());
Check(concurrent.Count(x=>x==200)==1 && concurrent.Count(x=>x==409)==1,"concurrent amendment serialised, no overlapping versions");
Console.WriteLine($"{checks} PostgreSQL repository/migration integration checks passed.");
