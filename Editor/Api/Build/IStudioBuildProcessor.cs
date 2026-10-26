namespace KitchenLib.Studio.Editor.Api.Build
{
    /// <summary>Produces bounded build-only replacements for existing authored scalar companion fields.</summary>
    public interface IStudioBuildProcessor
    {
        /// <summary>Stable namespaced ID. Studio captures this value once at registration.</summary>
        string Id { get; }
        /// <summary>Ascending execution order; ties use ordinal ID comparison. Captured at registration.</summary>
        int Order { get; }
        /// <summary>Returns replacements and findings without mutating authored data or writing files.</summary>
        StudioBuildProcessorResult Process(StudioBuildContext context);
    }
}
