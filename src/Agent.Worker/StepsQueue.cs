using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Expressions;
using Microsoft.VisualStudio.Services.Agent.Worker;
using Microsoft.VisualStudio.Services.Agent.Worker.Handlers;

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
        private readonly IHostContext context;
        private readonly JobStepsDebugger debugger;

        private sealed class LoggerDecorator : IPagingLogger
        {
            private readonly IHostContext context;
            private readonly IPagingLogger logger;
            private readonly StringBuilder buffer = new StringBuilder();

            public LoggerDecorator(IHostContext context, IPagingLogger logger)
            {
                this.context = context;
                this.logger = logger;
                logger.Setup(Guid.NewGuid(), Guid.NewGuid());
            }
            public void End()
            {
                logger.End();
                buffer.AppendLine("End");
            }

            public void Initialize(IHostContext context)
            {
                buffer.AppendLine("Initialize");
                logger.Initialize(context);
            }

            public void Setup(Guid timelineId, Guid timelineRecordId)
            {
                buffer.Append("Setup time=").AppendLine("" + timelineId);
                logger.Setup(timelineId, timelineRecordId);
            }

            public void Write(string message)
            {
                logger.Write(message);
                buffer.AppendLine(message);
            }

            public void Clear()
            {
                buffer.Clear();
            }

            public override string ToString()
            {
                return buffer.ToString();
            }
        }
        private readonly LoggerDecorator logger;

        public StepsQueue(IHostContext context, IExecutionContext executionContext, JobInitializeResult initializeResult) {
            this.developerMode = true;
            this.trace = context.GetTrace(nameof(Program));
            this.initializeResult = initializeResult;
            this.executionContext = executionContext;
            this.directoryManager = context.GetService<IBuildDirectoryManager>();
            this.context = context;

            if (developerMode) 
            {
                debugger = new JobStepsDebugger(context, executionContext);
                debugger.UpdateState(-1, initializeResult.JobSteps);
            }
            this.logger = new LoggerDecorator(context, context.CreateService<IPagingLogger>());

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
                Dictionary<string, string> newParameters = new Dictionary<string, string>();
                index = debugger.NextStep(executionContext.CancellationToken, newParameters);
                if (index < 0 || index >= initializeResult.JobSteps.Count)
                {
                    trace.Verbose($"Index: {index}, count: {initializeResult.JobSteps.Count}");
                    trace.Verbose($"Completed Jobs queue.  Total iterations: {iterations}");
                    //trace.Verbose($"Total log follows: ");
                    //trace.Verbose($"{logger.ToString()}");
                    //trace.Verbose($"End");
                    break;
                }
                if (last + 1 != index && last >= index)
                {
                    trace.Verbose($"Moving to step: {index}, from step: {last}");
                    directoryManager.RestoreDevelopmentSnapshot(executionContext, GetNameForStep(index - 1));
                }
                else if (last + 1 < index)
                {
                    int fill = last + 1;
                    while (fill < index)
                    {
                        trace.Verbose($"Filling in missing steps: {fill}");
                        directoryManager.SaveDevelopmentSnapshot(executionContext, GetNameForStep(fill));
                        fill++;
                    }
                }

                IStep current = initializeResult.JobSteps[index];
                if (current is ITaskRunner)
                {
                    ITaskRunner r = (ITaskRunner) current;
                    r.TaskInstance.Inputs.Clear();
                    foreach (string key in newParameters.Keys)
                    {
                        r.TaskInstance.Inputs.Add(key, newParameters[key]);
                    }
                }
                current.ExecutionContext.reset(logger);
                yield return current;
                iterations++;
                directoryManager.SaveDevelopmentSnapshot(executionContext, GetNameForStep(index));
                debugger.UpdateState(index, initializeResult.JobSteps);
                debugger.AppendLog(logger.ToString());
                logger.Clear();
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
    }
}

