using MediatR;
using System.Linq.Expressions;
using Viora.Application.Posts;
using Viora.Domain.Entities;

namespace Viora.Application.Articles;

public sealed record CreateArticleBlockRequest(
    int OrderIndex,
    ArticleBlockType Type,
    string? Content,
    string? MediaUrl,
    string? ThumbnailUrl,
    string? Caption);

public sealed record UpdateArticleBlockRequest(
    Guid? Id,
    int OrderIndex,
    ArticleBlockType Type,
    string? Content,
    string? MediaUrl,
    string? ThumbnailUrl,
    string? Caption);

public sealed record CreateArticleCommand(
    Guid UserId,
    string Title,
    PostVisibility Visibility,
    IReadOnlyList<CreateArticleBlockRequest> Blocks) : IRequest<ArticleResponse>;

public sealed record UpdateArticleCommand(
    Guid UserId,
    Guid ArticleId,
    string Title,
    PostVisibility Visibility,
    IReadOnlyList<UpdateArticleBlockRequest> Blocks) : IRequest<Result<ArticleResponse>>;

public sealed record GetArticleQuery(Guid UserId, Guid ArticleId)
    : IRequest<Result<ArticleResponse>>;

public sealed record GetMyArticlesQuery(Guid UserId, int Page, int PageSize)
    : IRequest<Result<MyArticlesResponse>>;

public sealed record ManagedArticleResponse(
    Guid Id, string Title, PostStatus Status, DateTime CreatedAt,
    string? ThumbnailUrl, int ViewCount, bool IsOwner);

public sealed record MyArticlesResponse(
    int Page, int PageSize, int TotalItems, int TotalPages,
    IReadOnlyList<ManagedArticleResponse> Items);

public sealed record ArticleBlockResponse(
    Guid Id,
    int OrderIndex,
    ArticleBlockType Type,
    string? Content,
    string? MediaUrl,
    string? ThumbnailUrl,
    string? Caption,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ArticleAuthorResponse(
    Guid Id,
    string DisplayName,
    string? AvatarUrl,
    bool IsVerified,
    AccountStyle AccountStyle);

public sealed record ArticleResponse(
    Guid Id,
    string Title,
    PostVisibility Visibility,
    PostStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int ReadingTimeMinutes,
    string? ThumbnailUrl,
    string? Preview,
    int ReactionCount,
    int CommentCount,
    int ShareCount,
    int SaveCount,
    int ViewCount,
    bool IsOwner,
    ArticleAuthorResponse Author,
    IReadOnlyList<ArticleBlockResponse> Blocks);

public interface IArticleRepository
{
    Task<ArticlePublicationAccount?> GetPublicationAccountAsync(Guid userId, CancellationToken cancellationToken);
    Task<MyArticlesResponse> GetMineAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task AddAsync(Post article, CancellationToken cancellationToken);
    Task<Post?> GetForUpdateAsync(Guid articleId, CancellationToken cancellationToken);
    Task PrepareBlockOrderUpdateAsync(Post article, CancellationToken cancellationToken);
    Task<bool> RecordViewAsync(Guid userId, Guid articleId, CancellationToken cancellationToken);
    Task<Result<ArticleResponse>> GetAsync(Guid userId, Guid articleId, CancellationToken cancellationToken);
}

public sealed record ArticlePublicationAccount(AccountStyle AccountStyle, bool CanCreateArticle)
{
    public bool CanPublish => ArticleCapabilities.CanPublish(AccountStyle);
    public bool CanManage => CanPublish || CanCreateArticle;
}

public static class ArticleAccess
{
    public static Expression<Func<Post, bool>> OwnArticles(Guid userId) => post =>
        post.UserId == userId && post.PostType == PostType.Article &&
        post.Status == PostStatus.Published && post.DeletedAt == null;

    public static bool CanView(PostStatus status, bool isOwner, PostVisibility visibility, bool isFollower) =>
        status != PostStatus.Deleted &&
        (isOwner || (status == PostStatus.Published &&
            (visibility == PostVisibility.Public || (visibility == PostVisibility.Followers && isFollower))));
}

public static class ArticleReadingTime
{
    private const int WordsPerMinute = 200;

    public static int Calculate(IEnumerable<string?> content)
    {
        var words = content
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Sum(value => value!.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
        return Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute));
    }
}
