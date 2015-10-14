namespace Slingshot.Models
{
    public interface IPullRequestInfo
    {
        dynamic RawContent { get; }

        string SourceBranch { get; }

        void AppendNewLineToDescription(string text);
    }
}
