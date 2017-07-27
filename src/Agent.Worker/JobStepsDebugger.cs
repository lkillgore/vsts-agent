using Microsoft.VisualStudio.Services.Agent.Worker.Build;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.TeamFoundation.DistributedTask.Orchestration.Server.Expressions;
using Microsoft.VisualStudio.Services.Agent.Worker;
using Microsoft.VisualStudio.Services.Agent.Worker.Handlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.VisualStudio.Services.Agent.Worker
{
    public class JobStepsDebugger
    {
        private Process nodeDebugger;
        private HttpClient debuggerClient; 

        public JobStepsDebugger(IHostContext context, IExecutionContext executionContext) 
        {
            Start();
        }

        private void Start() 
        {
            //TODO remove
            Debugger.Launch();

            if (debuggerClient == null)
            {
                debuggerClient = new HttpClient();
                debuggerClient.BaseAddress = new Uri("http://127.0.0.1:7777/");
            }

            if (nodeDebugger == null) 
            {
                RunAsync();
            }
        }

        private async void RunAsync() 
        {
            await Task.Run(() => {
                nodeDebugger = CreateNodeProcess();
                nodeDebugger.Start();
                nodeDebugger.WaitForExit();
            });            
        }

        private Process CreateNodeProcess()
        {
            string binFolder = Assembly.GetEntryAssembly().Location;
            string nodeBinFolder = Path.Combine(Path.GetDirectoryName(binFolder), "..", "externals", "node", "bin");
            string serverJS = Path.Combine(Path.GetDirectoryName(binFolder), "debugger", "server.js");
            string nodeExe = Path.Combine(nodeBinFolder, "node.exe");
            Process newProcess = new Process();
            newProcess.StartInfo = new ProcessStartInfo(nodeExe, serverJS);
            newProcess.StartInfo.CreateNoWindow = true;
            newProcess.StartInfo.UseShellExecute = false;
            newProcess.StartInfo.RedirectStandardInput = false;
            newProcess.StartInfo.RedirectStandardOutput = false;
            newProcess.StartInfo.RedirectStandardError = false;
            return newProcess;
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

        public int NextStep(CancellationToken cancellationToken, IDictionary<string, string> inputsForStep)
        {
            int step = -1;
            while (!cancellationToken.IsCancellationRequested && step < 0)
            {
                if (!cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
                {
                    HttpResponseMessage httpResponse = debuggerClient.GetAsync("http://127.0.0.1:7777/next").Result;
                    if (httpResponse.StatusCode == HttpStatusCode.OK) {
                        string content = httpResponse.Content.ReadAsStringAsync().Result;

                        JObject jsonObject = JObject.Parse(content);
                        Trace.TraceInformation($"Returned from UI: {jsonObject}");
                        step = jsonObject.GetValue("next").ToObject<int>();
                        IDictionary<string, JToken> properties = (JObject) jsonObject.GetValue("parameters");

                        foreach (string key in properties.Keys)
                        {
                            inputsForStep[key] = properties[key].Value<string>();
                        }
                    }
                    else
                    {
                        Trace.TraceWarning($"Got unexpected response from UI server: ({httpResponse.StatusCode}, {httpResponse.Content.ReadAsStringAsync().Result})");
                    }
                }
            }
            return step;
        }

        public void AppendLog(string log)
        {
            // debuggerClient.PostAsync<DebuggerState>("http://127.0.0.1:7777/appendconsole", log);
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

