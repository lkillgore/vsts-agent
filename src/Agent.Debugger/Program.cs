using System;

namespace Agent.Debugger
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("What do you want to do?");
            Environment.Exit(1);
            // var state = ReadState(args[0]);
            // DisplayCurrentState(state);
            // var action = GetAction(state);
            // SendAction(action);
        }

        private static void SendAction(Action action)
        {
            int exitCode = 0;
            int nextTask = action.Data == null ? 0 : int.Parse(action.Data);
            switch(action.Type)
            {
                case ActionType.SetNext: exitCode = nextTask; break;
                case ActionType.StepInto: exitCode = nextTask; break;
                case ActionType.StepOver: exitCode = nextTask; break;
                case ActionType.StopDebugging: exitCode = -1; break;
            }
            Environment.Exit(exitCode);
        }

        static Action GetAction(State state)
        {
            ActionType type = ActionType.Unknown;
            Console.WriteLine("What do you want to do?");
            while (true)
            {
                Console.WriteLine("Step (I)nto, Step (O)ver, Set Next (T)ask, (S)top debugging");
                String action = Console.ReadLine();
                switch (action.ToLower())
                {
                    case "i": type = ActionType.StepInto; break;
                    case "o": type = ActionType.StepOver; break;
                    case "t": type = ActionType.SetNext; break;
                    case "s": type = ActionType.StopDebugging; break;
                }

                if (type != ActionType.Unknown) break;
            }
            String data = null;
            if (type == ActionType.SetNext)
            {
                Console.Write("Which task?");

                for (int i = 0; i < state.Tasks.Length; i++)
                {
                    Console.Write("%1 (%2), ", state.Tasks[i], i);
                }
                Console.WriteLine("Enter the index of the task you would like to run next:");
                data = Console.ReadLine();
            }
            else
            {
                // just go to the next task
                data = (state.CurrentTask + 1).ToString();
            }
            return new Action(type, data);
        }

        private static void DisplayCurrentState(State state)
        {
            Console.Out.WriteLine(String.Join(", ", state.Tasks));
            Console.Out.WriteLine("Current task: ", state.Tasks[state.CurrentTask]);

            Console.Out.WriteLine("-----------------------------------------------------");
            foreach(String line in state.ConsoleOutput)
            {
                Console.Out.WriteLine(line);
            }
            Console.Out.WriteLine("-----------------------------------------------------");
        }

        static State ReadState(String incoming)
        {
            String[] lines = incoming.Split(new char[] { '|', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            String[] tasks = lines[0].Split(',');
            int currentTask = int.Parse(lines[1]);
            String[] output = new String[lines.Length - 2];
            Array.Copy(lines, 2, output, 0, output.Length);
            return new State(tasks, currentTask, output);
        }
    }

    public class State
    {
        public String[] Tasks { get; private set; }
        public int CurrentTask { get; private set; }
        public String[] ConsoleOutput { get; private set; }

        public State(String[] tasks, int currentTask, String[] consoleOutput)
        {
            Tasks = tasks;
            CurrentTask = currentTask;
            ConsoleOutput = consoleOutput;
        }
    }

    public enum ActionType
    {
        StopDebugging,
        StepOver,
        StepInto,
        SetNext,
        Unknown,
    }

    public class Action 
    {
        public ActionType Type { get; private set; }
        public String Data { get; private set; }

        public Action(ActionType type, String data = null)
        {
            Type = type;
            Data = data;
        }
    }
}