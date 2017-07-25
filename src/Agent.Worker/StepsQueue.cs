using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Text;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    [ServiceLocator(Default = typeof(StepsQueue))]

    public interface IStepsQueue
    {
        IEnumerable<IStep> GetPreJobSteps();
        IEnumerable<IStep> GetJobSteps();
        IEnumerable<IStep> GetPostJobSteps();
    }

    public class StepsQueue : IStepsQueue
    {
        private readonly JobInitializeResult initializeResult;
        private readonly IExecutionContext executionContext;
        private readonly Tracing trace;
        private readonly bool developerMode;
        private readonly IBuildDirectoryManager directoryManager;
        private int step = 0;
        
        public StepsQueue(IHostContext context, IExecutionContext executionContext, JobInitializeResult initializeResult) {
            this.developerMode = true;
            this.trace = context.GetTrace(nameof(Program));
            this.initializeResult = initializeResult;
            this.executionContext = executionContext;
            this.directoryManager = context.GetService<IBuildDirectoryManager>();

            // trace out all steps
            trace.Info($"Total pre-job steps: {initializeResult.PreJobSteps.Count}.");
            trace.Verbose($"Pre-job steps: '{string.Join(", ", initializeResult.PreJobSteps.Select(x => x.DisplayName))}'");

            trace.Info($"Total job steps: {initializeResult.JobSteps.Count}.");
            trace.Verbose($"Job steps: '{string.Join(", ", initializeResult.JobSteps.Select(x => x.DisplayName))}'");

            trace.Info($"Total post-job steps: {initializeResult.PostJobStep.Count}.");
            trace.Verbose($"Post-job steps: '{string.Join(", ", initializeResult.PostJobStep.Select(x => x.DisplayName))}'");
        }

        public void NextStep()
        {
        }

        public void RepeatStep(IStep next)
        {
            // Restore state
        }
        
        public IEnumerable<IStep> GetJobSteps()
        {
            if (developerMode)
            {
                return GetJobStepsEnumerator();
            }
            return initializeResult.JobSteps;
        }

        private IEnumerable<IStep> GetJobStepsEnumerator()
        {
            int iterations = 0;
            int index = 0;
            while(true)
            {
                // Ask the user which task to run next
                index = GetNextTask(index);
                if (index < 0 || index > initializeResult.JobSteps.Count)
                {
                    trace.Verbose($"Completed Jobs queue.  Total iterations: {iterations}");
                    break;
                }
                yield return initializeResult.JobSteps[index];
                iterations++;
                directoryManager.SaveDevelopmentSnapshot(executionContext, "step_" + index);
            }

            /*

            foreach (IStep s in initializeResult.JobSteps)
            {
                if (s == null)
                {
                    trace.Verbose($"Completed Jobs queue.  Total steps: {step}");
                    break;
                }

                trace.Verbose($"Current job{s.DisplayName}");
                yield return s;
                trace.Verbose($"Completed job{s.DisplayName}: saving state");
                step++;
                directoryManager.SaveDevelopmentSnapshot(executionContext, "step_" + step);
            }
            */
        }

        public IEnumerable<IStep> GetPostJobSteps()
        {
            return initializeResult.PostJobStep;
        }

        public IEnumerable<IStep> GetPreJobSteps()
        {
            return initializeResult.PreJobSteps;
        }

        public int GetNextTask(int currentTaskIndex)
        {
            Process process;
            StringBuilder args = new StringBuilder();
            for(int i = 0; i < initializeResult.JobSteps.Count; i++)
            {
                if (i > 0)
                {
                    args.Append(",");
                }
                args.Append(initializeResult.JobSteps[i].DisplayName);
            }
            args.Append("|");
            args.Append(currentTaskIndex.ToString());
            args.Append("|");
            args.Append("put console output here.");

            ProcessStartInfo startInfo = new ProcessStartInfo("Agent.Debugger.exe", args.ToString());
            process = Process.Start(startInfo);
            process.WaitForExit();
            return process.ExitCode;
        }
    }
}

