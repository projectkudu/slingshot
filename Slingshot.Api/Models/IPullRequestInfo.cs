namespace Slingshot.Models
{
    public interface IPullRequestInfo
    {
        string SourceBranch { get; }

        void AppendNewLineToDescription(string text);
    }
}
