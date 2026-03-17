using System.Threading.Tasks;

namespace QuestceSpire.Tracking;

/// <summary>
/// Common interface for all data pipelines.
/// Enables uniform orchestration, status tracking, and settings integration.
/// </summary>
public interface IDataPipeline
{
	/// <summary>Pipeline display name for logging and UI.</summary>
	string Name { get; }

	/// <summary>Execution phase (0=infra, 1=external, 2=local analysis, 3=runtime, 4=derived).</summary>
	int Phase { get; }

	/// <summary>Whether this pipeline requires network access.</summary>
	bool RequiresNetwork { get; }

	/// <summary>Run the pipeline. Returns true on success.</summary>
	Task<bool> RunAsync();

	/// <summary>Last run status for UI display.</summary>
	PipelineStatus LastStatus { get; }

	/// <summary>Last run duration in milliseconds.</summary>
	long LastDurationMs { get; }

	/// <summary>Last error message if failed.</summary>
	string LastError { get; }
}

public enum PipelineStatus
{
	NotRun,
	Running,
	Success,
	Failed,
	Skipped
}
