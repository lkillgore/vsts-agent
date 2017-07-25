using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Expressions;

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
        private sealed class FinalStep : IStep
        {
            public static readonly string FinalStepName = "StepsQueueFinalStep";

            public FinalStep()
            {
                DisplayName = FinalStepName;
            }

            public INode Condition { get; set; }
            public bool ContinueOnError => false;
            public string DisplayName { get; private set; }
            public bool Enabled => true;
            public IExecutionContext ExecutionContext { get; set; }
            public TimeSpan? Timeout => null;

            public Task RunAsync()
            {
                var tcs = new TaskCompletionSource<int>();
                tcs.SetException(new NotImplementedException());
                return tcs.Task;
            }
        }
        private readonly JobInitializeResult initializeResult;
        private readonly IExecutionContext executionContext;
        private readonly Tracing trace;
        private readonly bool developerMode;
        private readonly IBuildDirectoryManager directoryManager;

        private BlockingCollection<IStep> jobQueue;
        private IEnumerator<IStep> jobStepEnumerator;
        private readonly Stack<IStep> completedJobSteps = new Stack<IStep>();
        private int total = 0;
        
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
            if (jobStepEnumerator.MoveNext())
            {
                jobQueue.Add(jobStepEnumerator.Current);
            }
            else
            {
                jobQueue.Add(new FinalStep());
            }
        }

        public void RepeatStep(IStep next)
        {
            directoryManager.RestoreDevelopmentSnapshot(executionContext, GetNameForStep(completedJobSteps.Count));
            completedJobSteps.Pop(); // Delete last after the line above
            jobQueue.Add(next);
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
            /*
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

            */

            using (jobQueue = new BlockingCollection<IStep>())
            {
                jobStepEnumerator = initializeResult.JobSteps.GetEnumerator();

                IStep current = null;
                while (true)
                {
                    current = jobQueue.Take(executionContext.CancellationToken);
                    if (current == null || current.DisplayName.Equals(FinalStep.FinalStepName))
                    {
                        trace.Verbose($"Completed Jobs queue.  Total steps: {total}");
                        break;
                    }
                    trace.Verbose($"Current job{current.DisplayName}");
                    yield return current;
                    trace.Verbose($"Completed job{current.DisplayName}: saving state");
                    completedJobSteps.Push(current);
                    total++;
                    directoryManager.SaveDevelopmentSnapshot(executionContext, GetNameForStep(completedJobSteps.Count));
                }
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

