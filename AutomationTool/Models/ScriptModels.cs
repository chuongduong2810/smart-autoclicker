using System.Text.Json.Serialization;

namespace AutomationTool.Models
{
    public class AutomationScript
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
        public List<ScriptStep> Steps { get; set; } = new List<ScriptStep>();
        public bool IsActive { get; set; } = false;
        
        // Repeat configuration
        public bool IsInfiniteRepeat { get; set; } = false;
        public int RepeatCount { get; set; } = 1;
        public int DelayBetweenRepeats { get; set; } = 0; // Milliseconds delay between repetitions
    }

    public class ScriptStep
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int Order { get; set; }
        public string Type { get; set; } = string.Empty; // "condition", "action", "wait", "jump"
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public List<ScriptCondition> Conditions { get; set; } = new List<ScriptCondition>();
        public List<ScriptAction> Actions { get; set; } = new List<ScriptAction>();
        public string? ElseStepId { get; set; } // Step to jump to if conditions fail
        public bool IsEnabled { get; set; } = true;
    }

    public class ScriptCondition
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty; // "image_found", "image_not_found", "timeout", "always"
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string Operator { get; set; } = "AND"; // "AND", "OR"
    }

    public class ScriptAction
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = string.Empty; // "click", "type", "wait", "jump", "screenshot"
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public int DelayAfter { get; set; } = 0; // Milliseconds to wait after action
    }

    public class TemplateImage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public Rectangle CaptureRegion { get; set; }
        public double MatchThreshold { get; set; } = 0.8;
    }

    public class ScreenRegion
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Rectangle ToRectangle() => new Rectangle(X, Y, Width, Height);
    }

    public class MatchResult
    {
        public bool Found { get; set; }
        public Point Location { get; set; }
        public double Confidence { get; set; }
        public Rectangle BoundingBox { get; set; }
        public TimeSpan SearchTime { get; set; }
    }

    public class ExecutionLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string ScriptId { get; set; } = string.Empty;
        public string StepId { get; set; } = string.Empty;
        public string Level { get; set; } = "INFO"; // INFO, WARN, ERROR, DEBUG
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object>? Data { get; set; }
    }

    public class ScriptExecutionState
    {
        public string ScriptId { get; set; } = string.Empty;
        public string CurrentStepId { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public string Status { get; set; } = "STOPPED"; // RUNNING, PAUSED, STOPPED, COMPLETED, ERROR
        public List<ExecutionLog> Logs { get; set; } = new List<ExecutionLog>();
        public int LoopCount { get; set; } = 0;
        public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
        
        // Repeat execution tracking
        public int CurrentRepeat { get; set; } = 0;
        public int TotalRepeats { get; set; } = 1;
        public bool IsInfiniteRepeat { get; set; } = false;
        public DateTime? LastRepeatTime { get; set; }
    }

    // Enums for better type safety
    public enum ActionType
    {
        Click,
        DoubleClick,
        RightClick,
        Type,
        KeyPress,
        Wait,
        Jump,
        Screenshot,
        MoveMouse
    }

    public enum ConditionType
    {
        ImageFound,
        ImageNotFound,
        Timeout,
        Always,
        Never,
        VariableEquals,
        VariableNotEquals
    }

    public enum ExecutionStatus
    {
        Stopped,
        Running,
        Paused,
        Completed,
        Error
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}