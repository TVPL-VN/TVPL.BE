using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Viora.Application.Statutory;
using Viora.Domain.Entities;
using Viora.Infrastructure.Persistence;
using Viora.Infrastructure.Persistence.Repositories;

const string connection = "Host=127.0.0.1;Port=55439;Database=statutory_correction_check;Username=statutory_test;Timeout=5";
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
await db.Database.MigrateAsync();
var account = new Account { Id=Guid.NewGuid(), Email=$"correction-{Guid.NewGuid()}@example.test", Role=AccountRole.Admin, Status=AccountStatus.Active };
var actor = new User { Id=Guid.NewGuid(), AccountId=account.Id, DisplayName="Admin correction check" };
db.Add(account); db.Add(actor); await db.SaveChangesAsync();
var checks=0; var repo=new StatutoryRepository(db);
void Check(bool value,string label){if(!value)throw new Exception(label);checks++;}
async Task Reject(Func<Task> action,int status,string label){try{await action();throw new Exception("Expected rejection: "+label);}catch(StatutoryException e){Check(e.Status==status,label);db.ChangeTracker.Clear();}}
var authority=await repo.AddCatalogAsync(true,"Correction check",default);
var date=DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7)).AddDays(-100);
async Task<StatutorySummary> Document()=>await repo.SaveAsync(null,actor.Id,new("Correction test",Guid.NewGuid().ToString(),StatutoryType.Law,authority.Id,date.AddDays(-1),date,null,false,null,null,null,[]),default);
SaveSectionRequest Section(Guid? parent,LegalNodeType type,string number,int order=0,string text="Nội dung gốc")=>new(parent,type,number,$"Mục {number}",order,text);
DataCorrectionRequest Correction(Guid? parent,LegalNodeType type,string number,int order=0,string text="Nội dung bổ sung",Guid? version=null,string reason="Bổ sung dữ liệu còn thiếu khi số hóa văn bản")=>new(parent,type,number,$"Mục {number}",order,text,reason,version);

// Case 1: draft flow is still ordinary CRUD.
var doc=await Document();
var chapter=await repo.SaveSectionAsync(doc.Id,null,actor.Id,Section(null,LegalNodeType.Chapter,"I"),default);
var one=await repo.SaveSectionAsync(doc.Id,null,actor.Id,Section(chapter.Id,LegalNodeType.Article,"1"),default);
await repo.SaveSectionAsync(doc.Id,one.Id,actor.Id,Section(chapter.Id,LegalNodeType.Article,"1",0,"Bản nháp đã sửa"),default);
var leaf=await repo.SaveSectionAsync(doc.Id,null,actor.Id,Section(chapter.Id,LegalNodeType.Article,"9",9),default);
await repo.DeleteSectionAsync(leaf.Id,default);
Check(!await db.LegalSections.AnyAsync(x=>x.Id==leaf.Id),"draft leaf deletion");
await Reject(()=>repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(null,LegalNodeType.Chapter,"II"),default),409,"draft cannot use correction endpoint");
var two=await repo.SaveSectionAsync(doc.Id,null,actor.Id,Section(chapter.Id,LegalNodeType.Article,"2",1),default);
var four=await repo.SaveSectionAsync(doc.Id,null,actor.Id,Section(chapter.Id,LegalNodeType.Article,"4",2),default);
await repo.PublishAsync(doc.Id,actor.Id,true,default);
// Cases 2,3: published original completion, ordering and audit, never an amendment.
var three=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"3",2),default);
var detail=(await repo.DetailAsync(doc.Id,true,null,default))!;
Check(string.Join(',',detail.Sections.Where(x=>x.Type==LegalNodeType.Article).OrderBy(x=>x.Order).Select(x=>x.Number))=="1,2,3,4","insert shifts following siblings");
Check(three.CurrentVersion is {VersionNumber:1,ChangeType:LegalChangeType.Original,ChangedByDocumentId:null} && three.CurrentVersion.ValidFrom==date,"only original baseline, original effective date");
Check(!await db.StatutoryDocumentRelations.AnyAsync(x=>x.DocumentId==doc.Id||x.RelatedDocumentId==doc.Id),"no amendment relation");
var audit=await db.AdminLogs.SingleAsync(x=>x.TargetId==three.Id&&x.Action=="AddMissingSection");
using(var json=JsonDocument.Parse(audit.Description!)){Check(json.RootElement.GetProperty("DocumentId").GetGuid()==doc.Id&&json.RootElement.GetProperty("ParentSectionId").GetGuid()==chapter.Id&&audit.AdminId==actor.Id,"completion audit identifiers and actor");}
// Case 4: missing chapter and all seven node types.
var second=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(null,LegalNodeType.Chapter,"II",1),default);
var secondArticle=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(second.Id,LegalNodeType.Article,"10"),default);
var clause=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(secondArticle.Id,LegalNodeType.Clause,"2") with {Title=""},default);
var point=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(clause.Id,LegalNodeType.Point,"a") with {Title=""},default);
Check(point.ParentId==clause.Id,"published chapter, article, untitled clause and point completion");
var part=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(null,LegalNodeType.Part,"II",2),default);
var ch=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(part.Id,LegalNodeType.Chapter,"III"),default);
var group=await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(ch.Id,LegalNodeType.Section,"1"),default);
await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(group.Id,LegalNodeType.Subsection,"1"),default);
Check(await db.LegalSections.CountAsync(x=>x.DocumentId==doc.Id&&x.Type<=LegalNodeType.Subsection)==6,"all organizational levels");
// Case 5: required reason, old text preserved in full audit, legal intervals unchanged.
var before=await db.LegalSectionVersions.AsNoTracking().SingleAsync(x=>x.SectionId==three.Id);
await Reject(()=>repo.CorrectSectionAsync(doc.Id,three.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"3",2,"Sai",before.Id," "),default),400,"blank correction reason rejected");
var largeText=new string('x',5000)+" bản số hóa đã hiệu chỉnh";
await repo.CorrectSectionAsync(doc.Id,three.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"3",2,largeText,before.Id,"Sai nội dung khi số hóa"),default);
var after=await db.LegalSectionVersions.AsNoTracking().SingleAsync(x=>x.SectionId==three.Id);
Check(after.Id==before.Id&&after.ValidFrom==before.ValidFrom&&after.ValidTo==before.ValidTo&&after.ChangeType==before.ChangeType,"correction preserves legal version identity and dates");
audit=await db.AdminLogs.SingleAsync(x=>x.TargetId==three.Id&&x.Action=="CorrectSectionData");
using(var json=JsonDocument.Parse(audit.Description!)) {
    Check(json.RootElement.GetProperty("After").GetProperty("Version").GetProperty("Content").GetString()==largeText &&
        json.RootElement.GetProperty("Before").GetProperty("Version").GetProperty("Content").GetString()==before.Content,"untruncated before/after audit");
}
await repo.CorrectSectionAsync(doc.Id,second.Id,actor.Id,Correction(null,LegalNodeType.Chapter,"IV",1,"Nội dung bổ sung",second.CurrentVersion!.Id,"Sửa số chương nhập sai"),default);
Check((await db.LegalSections.AsNoTracking().SingleAsync(x=>x.Id==point.Id)).Path.StartsWith("1:IV/"),"renumbered subtree paths remain consistent");
// Case 6: real amendment continues to create versions and preserve old text.
var cause=await Document(); await repo.SaveSectionAsync(cause.Id,null,actor.Id,Section(null,LegalNodeType.Article,"1"),default); await repo.PublishAsync(cause.Id,actor.Id,true,default);
await repo.AmendAsync(one.Id,actor.Id,new("Nội dung pháp luật mới",date.AddDays(10),cause.Id,LegalChangeType.Amended,"Căn cứ văn bản sửa đổi",true),default);
Check((await repo.VersionsAsync(one.Id,true,default)).Count==2,"real amendment adds legal version");
Check((await repo.DetailAsync(doc.Id,false,date.AddDays(1),default))!.Sections.Single(x=>x.Id==one.Id).CurrentVersion!.Content=="Bản nháp đã sửa","historical legal content remains accessible");
// Cases 7,8: no old-endpoint bypass, duplicates or lost order after reload.
await Reject(()=>repo.SaveSectionAsync(doc.Id,three.Id,actor.Id,Section(chapter.Id,LegalNodeType.Article,"3"),default),409,"published old update rejected");
await Reject(()=>repo.SaveSectionAsync(doc.Id,null,actor.Id,Section(chapter.Id,LegalNodeType.Article,"8"),default),409,"published old create rejected");
await Reject(()=>repo.DeleteSectionAsync(three.Id,default),409,"published old delete rejected");
await Reject(()=>repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"3",2),default),409,"duplicate supplement rejected");
detail=(await repo.DetailAsync(doc.Id,false,null,default))!;
Check(detail.Sections.Count(x=>x.Number=="3"&&x.ParentId==chapter.Id)==1,"reload has exactly one inserted article");
Check(string.Join(',',detail.Sections.Where(x=>x.ParentId==chapter.Id).OrderBy(x=>x.Order).Select(x=>x.Number))=="1,2,3,4","reload retains order");
await repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"2a",2),default);
Check((await repo.DetailAsync(doc.Id,true,null,default))!.Sections.Where(x=>x.ParentId==chapter.Id).OrderBy(x=>x.Order).Select(x=>x.Number).SequenceEqual(new[]{"1","2","2a","3","4"}),"noninteger identifiers and ordered insertion");
await Reject(()=>repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Point,"z"),default),400,"invalid child hierarchy rejected");
await Reject(()=>repo.CorrectSectionAsync(doc.Id,three.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"3",2,"Bad",Guid.NewGuid()),default),409,"unrelated version cannot be corrected");
await repo.PublishAsync(doc.Id,actor.Id,false,default);
await Reject(()=>repo.DeleteSectionAsync(three.Id,default),409,"unpublishing cannot bypass protections");
var auditCount=await db.AdminLogs.CountAsync(x=>x.AdminId==actor.Id);
await Reject(()=>repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"3",0),default),409,"duplicate cannot shift siblings or write audit");
Check(await db.AdminLogs.CountAsync(x=>x.AdminId==actor.Id)==auditCount,"rejected corrections are transactional");
await db.Accounts.Where(x=>x.Id==account.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Role,AccountRole.User));
await Reject(()=>repo.AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"20"),default),403,"nonadmin cannot use correction repository");
await db.Accounts.Where(x=>x.Id==account.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Role,AccountRole.Admin));
var versionHistory=await repo.VersionsAsync(one.Id,true,default);
var current=versionHistory.Last();
await repo.CorrectSectionAsync(doc.Id,one.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"1",0,"Hiệu chỉnh bản số hóa của phiên bản sửa đổi",current.Id,"Lỗi số hóa phiên bản hiện tại"),default);
var correctedHistory=await repo.VersionsAsync(one.Id,true,default);
Check(versionHistory.Select(v=>(v.Id,v.ValidFrom,v.ValidTo,v.ChangeType,v.ChangedByDocumentId)).SequenceEqual(correctedHistory.Select(v=>(v.Id,v.ValidFrom,v.ValidTo,v.ChangeType,v.ChangedByDocumentId))),"correcting an amended text leaves the complete legal timeline unchanged");
Check(correctedHistory.First().Content==versionHistory.First().Content,"other legal versions are untouched");
async Task<int> Race() {
    await using var isolated=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
    try {await new StatutoryRepository(isolated).AddMissingSectionAsync(doc.Id,actor.Id,Correction(chapter.Id,LegalNodeType.Article,"30",5),default);return 200;}
    catch(StatutoryException e){return e.Status;}
}
var race=await Task.WhenAll(Race(),Race());
Check(race.Count(x=>x==200)==1&&race.Count(x=>x==409)==1,"concurrent duplicate supplements are serialized");
var racedSection=await db.LegalSections.SingleAsync(x=>x.DocumentId==doc.Id&&x.ParentId==chapter.Id&&x.Number=="30");
Check(await db.AdminLogs.CountAsync(x=>x.AdminId==actor.Id&&x.Action=="AddMissingSection"&&x.TargetId==racedSection.Id)==1,"successful concurrent completion is audited exactly once");
var legacyDoc=await Document();
var legacySection=await repo.SaveSectionAsync(legacyDoc.Id,null,actor.Id,Section(null,LegalNodeType.Article,"1"),default);
await db.StatutoryDocuments.Where(x=>x.Id==legacyDoc.Id).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.IsPublished,true));
db.ChangeTracker.Clear();
await Reject(()=>repo.DeleteSectionAsync(legacySection.Id,default),409,"legacy published flag cannot bypass delete protection");
Console.WriteLine($"{checks} published data correction PostgreSQL checks passed.");
