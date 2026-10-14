namespace Viora.Domain.Entities;

public static class ArticleCapabilities
{
    public static bool CanPublish(User user) => user.CanCreateArticle;

    public static bool CanManage(User user) => user.CanCreateArticle;
}
