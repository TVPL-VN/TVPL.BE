using Viora.Application.Statutory;
using Viora.Domain.Entities;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
Check(StatutoryRules.CanParent(null, LegalNodeType.Article), "root article");
Check(StatutoryRules.CanParent(LegalNodeType.Article, LegalNodeType.Clause), "article clause");
Check(StatutoryRules.CanParent(LegalNodeType.Clause, LegalNodeType.Point), "clause point");
Check(!StatutoryRules.CanParent(LegalNodeType.Chapter, LegalNodeType.Point), "point cannot be chapter child");
Check(!StatutoryRules.CanParent(null, LegalNodeType.Clause), "clause needs article");
Check(!StatutoryRules.CanParent(LegalNodeType.Point, LegalNodeType.Article), "no backwards nesting");
Check(StatutoryRules.Validity(new(2026, 10, 4), null, false, new(2026, 10, 3)) == StatutoryValidity.Upcoming, "future");
Check(StatutoryRules.Validity(new(2026, 10, 3), null, false, new(2026, 10, 3)) == StatutoryValidity.Effective, "inclusive start");
Check(StatutoryRules.Validity(new(2026, 1, 1), new(2026, 10, 3), false, new(2026, 10, 3)) == StatutoryValidity.Expired, "expiry exclusive");
Check(StatutoryRules.Validity(new(2026, 1, 1), null, true, new(2026, 10, 3)) == StatutoryValidity.PartiallyExpired, "partial");
Check(!StatutoryRules.SafeUrl("javascript:alert(1)"), "unsafe source");
Check(StatutoryRules.SafeUrl("https://example.org/law.pdf"), "https source");
Check(!StatutoryRules.SafeUrl("file:///etc/passwd"), "no file URI");
Console.WriteLine($"{checks} statutory business-rule checks passed.");
