namespace Viora.Domain.Entities;

public static class ArticleCapabilities
{
    public static bool CanPublish(AccountStyle style) =>
        style is AccountStyle.Lawyer or AccountStyle.LegalExpert or AccountStyle.LawFirm;

    public static bool CanManage(User user) => CanPublish(user.AccountStyle) || user.CanCreateArticle;
}
