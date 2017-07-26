using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Expressions;
using Microsoft.VisualStudio.Services.Agent.Worker;

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
        private readonly CancellationToken cancelToken;
        private readonly Tracing trace;
        private readonly bool developerMode;
        private readonly IBuildDirectoryManager directoryManager;

        public StepsQueue(IHostContext context, IExecutionContext executionContext, JobInitializeResult initializeResult, CancellationToken cancelToken) {
            this.developerMode = true;
            this.trace = context.GetTrace(nameof(Program));
            this.initializeResult = initializeResult;
            this.executionContext = executionContext;
            this.cancelToken = cancelToken;
            this.directoryManager = context.GetService<IBuildDirectoryManager>();

            // trace out all steps
            trace.Info($"Total pre-job steps: {initializeResult.PreJobSteps.Count}.");
            trace.Verbose($"Pre-job steps: '{string.Join(", ", initializeResult.PreJobSteps.Select(x => x.DisplayName))}'");

            trace.Info($"Total job steps: {initializeResult.JobSteps.Count}.");
            trace.Verbose($"Job steps: '{string.Join(", ", initializeResult.JobSteps.Select(x => x.DisplayName))}'");

            trace.Info($"Total post-job steps: {initializeResult.PostJobStep.Count}.");
            trace.Verbose($"Post-job steps: '{string.Join(", ", initializeResult.PostJobStep.Select(x => x.DisplayName))}'");
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
            directoryManager.SaveDevelopmentSnapshot(executionContext, GetNameForStep(index - 1));
            while(true)
            {
                // Ask the user which task to run next
                int last = index;
                index = GetNextTask(index);
                if (index < 0 || index >= initializeResult.JobSteps.Count)
                {
                    trace.Verbose($"Index: {index}, count: {initializeResult.JobSteps.Count}");
                    trace.Verbose($"Completed Jobs queue.  Total iterations: {iterations}");
                    break;
                }
                if (last + 1 != index && last >= index)
                {
                    trace.Verbose($"Moving to step: {index}, from step: {last}");
                    directoryManager.RestoreDevelopmentSnapshot(executionContext, GetNameForStep(index - 1));
                }

                IStep current = initializeResult.JobSteps[index];
                current.ExecutionContext.reset();
                yield return current;
                iterations++;
                directoryManager.SaveDevelopmentSnapshot(executionContext, GetNameForStep(index));
            }
        }

        private string GetNameForStep(int step)
        {
            return "step_" + step;
        }

        public IEnumerable<IStep> GetPostJobSteps()
        {
            return initializeResult.PostJobStep;
        }

        public IEnumerable<IStep> GetPreJobSteps()
        {
            return initializeResult.PreJobSteps;
        }

        int i = 0;
        public int GetNextTask(int currentTaskIndex)
        {
            int[] tasks = new int[] {0, 1, 0, 1, 2};
            return tasks[i++];
        }
    }
}

