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
    public class JobStepsDebugger
    {
        private NodeHandler nodeDebugger;
        private HttpClient debuggerClient; 

        public JobStepsDebugger(IHostContext context, IExecutionContext executionContext) 
        {
            Start(context, executionContext);
        }

        private void Start(IHostContext context, IExecutionContext executionContext) 
        {
            //TODO remove
            Debugger.Launch();

            if (nodeDebugger == null) 
            {
                nodeDebugger = (NodeHandler)context.CreateService<INodeHandler>();
                nodeDebugger.TaskDirectory = Path.Combine(context.GetDirectory(WellKnownDirectory.Bin), "debugger");
                nodeDebugger.Data = new NodeHandlerData() 
                {
                    Target = "server.js",
                    WorkingDirectory = "", 
                };
                nodeDebugger.ExecutionContext = executionContext;
                nodeDebugger.Inputs = new Dictionary<string, string>();
                nodeDebugger.RunAsync();
                debuggerClient = new HttpClient();
                debuggerClient.BaseAddress = new Uri("http://127.0.0.1:7777/");
            }
        }

        public void UpdateState(int currentTaskIndex, List<IStep> steps) 
        {
            var state = new DebuggerState() { current = currentTaskIndex, tasks = new DebuggerState.Task[steps.Count] };
            int taskCount = 0;
            foreach(IStep step in steps) 
            {
                var parameters = new Dictionary<String,String>();
                if (step is ITaskRunner)
                {
                    ITaskRunner r = (ITaskRunner) step;
                    foreach (string key in r.TaskInstance.Inputs.Keys)
                    {
                        parameters.Add(key, r.TaskInstance.Inputs[key]);
                    }
                }
                state.tasks[taskCount++] = new DebuggerState.Task() { name = step.DisplayName, parameters = parameters };
            }                                                                

            debuggerClient.PostAsJsonAsync<DebuggerState>("http://127.0.0.1:7777/update", state);
        }

        public class DebuggerState 
        {
            public class Task 
            {
                public String name { get; set; }
                public Dictionary<String,String> parameters { get; set; }
            }

            public Task[] tasks { get; set; }
            public int current { get; set; }
        }
    }
}

