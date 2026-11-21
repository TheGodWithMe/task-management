using TaskManagement.Contracts.Models;

namespace TaskManagement.WebClient.Models;

public class WorkspaceModel
{
    public List<ProjectResponse> Projects { get; set; } = new();
    public List<TaskResponse> Tasks { get; set; } = new();
    public List<UserResponse> Users { get; set; } = new();
    public List<ParticipantResponse> Participants { get; set; } = new();
    public TaskDetails? Details { get; set; }
    public ProjectReportResponse? Report { get; set; }
    public long? ProjectId { get; set; }
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime Start { get; set; } = DateTime.Today.AddMonths(-1);
    public DateTime End { get; set; } = DateTime.Today;
}

public class TaskDetails
{
    public TaskResponse Task { get; set; } = new();
    public List<CommentDetails> Comments { get; set; } = new();
    public List<TaskHistoryResponse> History { get; set; } = new();
}

public class CommentDetails
{
    public string Text { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
