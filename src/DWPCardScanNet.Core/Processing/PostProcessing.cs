using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DWPCardScanNet.Core.Processing
{
    /// <summary>
    /// Context handed to a post-processor after a group's NIST packet (and PDF, if any) is written.
    /// Mirrors the property surface the VB6 set on its PostProcessDLL COM object
    /// (ConfigFileName / NISTPacketPath / KeyValue / VerificationFile / StatusReportPath / TransactionType).
    /// </summary>
    public sealed class PostProcessContext
    {
        /// <summary>The group's PostProcessDLLcfg value (an arbitrary config path/string for the plugin).</summary>
        public string ConfigFileName { get; set; }

        /// <summary>Full path of the NIST/EBTS packet that was just written (.eft).</summary>
        public string NistPacketPath { get; set; }

        /// <summary>The group key (the group folder name).</summary>
        public string KeyValue { get; set; }

        /// <summary>The group's verification/template file.</summary>
        public string VerificationFile { get; set; }

        /// <summary>Path of the current run's group status report (may be null if none is configured).</summary>
        public string StatusReportPath { get; set; }

        /// <summary>The group's transaction type (TOT).</summary>
        public string TransactionType { get; set; }
    }

    /// <summary>Outcome of a post-processor run (port of the VB6 iRet / LastError / ProcessComplete surface).</summary>
    public sealed class PostProcessResult
    {
        /// <summary>False signals a failure; the group is recorded as failed and its folder is NOT removed.</summary>
        public bool Success { get; set; } = true;

        /// <summary>Error detail when <see cref="Success"/> is false.</summary>
        public string LastError { get; set; }

        /// <summary>
        /// When true (and the run is configured to delete groups), the group folder is removed after
        /// processing - the VB6 ProcessComplete flag. Set false to leave the group in place (e.g. the
        /// plugin has queued deferred work and will clean up later).
        /// </summary>
        public bool ProcessComplete { get; set; } = true;

        public static PostProcessResult Ok(bool processComplete = true) =>
            new PostProcessResult { Success = true, ProcessComplete = processComplete };

        public static PostProcessResult Fail(string error) =>
            new PostProcessResult { Success = false, LastError = error, ProcessComplete = false };
    }

    /// <summary>
    /// Managed replacement for the VB6 PostProcessDLL COM hook. Customers implement this in their own
    /// assembly to handle site-specific requirements (export, DB insert, notification, custom cleanup)
    /// after each group's packet is written. The implementing type must be public with a public
    /// parameterless constructor. Configure it via the group's PostProcessDLL / PostProcessDLLcfg .ini keys.
    /// No COM registration is required.
    /// </summary>
    public interface IPostProcessor
    {
        /// <summary>Called once per group after its NIST packet (and PDF, if any) is written.</summary>
        PostProcessResult PostProcess(PostProcessContext context);
    }

    /// <summary>Resolves and instantiates the configured <see cref="IPostProcessor"/> from a config spec.</summary>
    public interface IPostProcessorLoader
    {
        /// <summary>
        /// Loads the post-processor named by <paramref name="spec"/> (the group's PostProcessDLL value),
        /// or returns null when the spec is empty. Throws when a spec is given but cannot be resolved.
        /// </summary>
        IPostProcessor Load(string spec);
    }

    /// <summary>
    /// Default <see cref="IPostProcessorLoader"/>: loads a managed post-processor by reflection (no COM).
    /// Supported <c>PostProcessDLL</c> spec forms:
    /// <list type="number">
    /// <item><c>C:\path\MyPlugin.dll</c> - load the assembly and use the single public type implementing
    ///       <see cref="IPostProcessor"/>.</item>
    /// <item><c>C:\path\MyPlugin.dll!My.Namespace.Type</c> - load the assembly and use the named type
    ///       (use this when the assembly has more than one implementation).</item>
    /// <item><c>My.Namespace.Type, MyAssemblyName</c> - resolve via <see cref="Type.GetType(string)"/>
    ///       (the assembly must be alongside the app or otherwise probeable).</item>
    /// </list>
    /// Relative assembly paths are resolved against the application base directory.
    /// </summary>
    public sealed class AssemblyPostProcessorLoader : IPostProcessorLoader
    {
        public IPostProcessor Load(string spec)
        {
            if (string.IsNullOrWhiteSpace(spec)) return null;
            spec = spec.Trim();

            Type type = ResolveType(spec);
            if (type == null)
                throw new InvalidOperationException("PostProcessDLL type could not be resolved: " + spec);
            if (!typeof(IPostProcessor).IsAssignableFrom(type))
                throw new InvalidOperationException(
                    "PostProcessDLL type '" + type.FullName + "' does not implement IPostProcessor.");

            return (IPostProcessor)Activator.CreateInstance(type);
        }

        private static Type ResolveType(string spec)
        {
            // Form 2: "<assembly path>!<type name>"
            int bang = spec.IndexOf('!');
            if (bang > 0)
            {
                string asmPath = spec.Substring(0, bang).Trim();
                string typeName = spec.Substring(bang + 1).Trim();
                return LoadAssembly(asmPath).GetType(typeName, throwOnError: true, ignoreCase: false);
            }

            // Form 1: a ".dll" path -> the single IPostProcessor implementation in it.
            if (spec.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                Assembly asm = LoadAssembly(spec);
                var candidates = asm.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IPostProcessor).IsAssignableFrom(t))
                    .ToList();
                if (candidates.Count == 0)
                    throw new InvalidOperationException(
                        "Assembly '" + spec + "' contains no public IPostProcessor implementation.");
                if (candidates.Count > 1)
                    throw new InvalidOperationException(
                        "Assembly '" + spec + "' contains multiple IPostProcessor implementations; " +
                        "specify one as \"" + spec + "!Namespace.TypeName\".");
                return candidates[0];
            }

            // Form 3: an assembly-qualified .NET type name.
            return Type.GetType(spec, throwOnError: false, ignoreCase: false);
        }

        private static Assembly LoadAssembly(string path)
        {
            if (!Path.IsPathRooted(path))
            {
                string candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", path);
                if (File.Exists(candidate)) path = candidate;
            }
            if (!File.Exists(path))
                throw new FileNotFoundException("PostProcessDLL assembly not found: " + path, path);
            return Assembly.LoadFrom(path);
        }
    }
}
