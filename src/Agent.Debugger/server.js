var path = require('path');
var express = require('express');
var jsdom = require('jsdom');

var app = express();
app.set('port', 7777);
app.use(express.logger('dev'));  /* 'default', 'short', 'tiny', 'dev' */
app.use(express.bodyParser());

var state = {}
var nextTaskToExecute = 0
var consoleLines = [''];
var resumeExecution = false;

app.get('/', function (req, res) {
   res.send(getMainView());
})

app.post('/update', function (req, res) {
    setState(req.body);
    res.status(200);
    res.end();
})

app.post('/updateparameters', function (req, res) {
    let taskId = parseInt(req.body.taskId);
    let parameters = req.body.parameters ? JSON.parse(req.body.parameters) : {};
    updateAndContinue(taskId, parameters);
    res.redirect('/');
})

app.post('/setnext', function (req, res) {
    setNextTask(parseInt(req.body.id));
    res.redirect('/');
})

app.post('/appendconsole', function (req, res) {
    consoleLines = consoleLines.concat(req.body.lines);
    res.status(200);
    res.end();
})

app.get('/next', function (req, res) {
   res.json(getNext());
})

initialize();
app.listen(7777);

function initialize() {
    state = {
        tasks: [
            { name: "Loading...", parameters: { key1: "value1", key2: "value2" } }
        ],
        current: 0
    }
    nextTaskToExecute = 0;
    consoleLines = [''];
    resumeExecution = false;
}

function setState(newState) {
    setContinue(false);
    state = newState;
    setNextTask(state.current + 1);
}

function updateAndContinue(taskId, newParameters) {
    if (taskId >= 0) {
        if (state && state.tasks && state.tasks.length > taskId) {
            state.tasks[taskId].parameters = newParameters;
        }
    }
    setNextTask(taskId);
    setContinue(true);
}

function setNextTask(taskId) {
    if (taskId < 0) {
        // Stop debugging; set next to end+1
        nextTaskToExecute = state.tasks.length;
    }
    nextTaskToExecute = taskId;
}

function setContinue(shouldContinue) {
    resumeExecution = shouldContinue;
}

function getMainView() {
    let consoleOutput = consoleLines.join('\n');

    let nextTask = undefined;
    let nextTaskInstance = undefined;
    let htmlTasks = ''
    if (state && state.tasks) {
        nextTask = nextTaskToExecute >= 0 ? nextTaskToExecute : state.current + 1;
        let width = 100 / (state.tasks.length + 2);
        for(let i = 0; i < state.tasks.length; i++) {
            let name = state.tasks[i].name;
            let color = state.current === i ? 'blue' : 'darkblue';
            let hint = nextTask === i ? 'This is the next task that will execute when you hit continue':'Click here to make this the next task that will execute';
            let onclick = nextTask === i ? '' : `onclick="setNext(${i})"`
            let border = nextTask === i ? `border: 4px solid green;` : ''
            htmlTasks += `<td ${onclick} style="color:#FFFFFF;background:${color};${border}" width="${width}%" height="75px" title="${hint}">`;
            htmlTasks += `<div align="center">${name}</div></td><td class="sep">&rarr;</td>`;
            if (i === nextTask) {
                nextTaskInstance = state.tasks[i];
            }
        }
    }


    let html = `<html>
    <style media="screen" type="text/css">
        html, body {
            height: 100%;
            margin: 0px;
        }

        #wrapper:before {
            content:'';
            float: left;
            height: 100%;
        }
        #wrapper {
            height: 100%;
            background-color: black;
            color: white;
        }
        #header {
            background-color:#000;
        }
        #content {
            background-color: gray;
            padding-top: 20px;
        }
        #content:after {
            content:'';
            display: block;
            clear: both;
        }

        .btn {
            background: #3498db;
            background-image: -webkit-linear-gradient(top, #3498db, #2980b9);
            background-image: -moz-linear-gradient(top, #3498db, #2980b9);
            background-image: -ms-linear-gradient(top, #3498db, #2980b9);
            background-image: -o-linear-gradient(top, #3498db, #2980b9);
            background-image: linear-gradient(to bottom, #3498db, #2980b9);
            -webkit-border-radius: 28;
            -moz-border-radius: 28;
            border-radius: 28px;
            font-family: Arial;
            color: #ffffff;
            font-size: 20px;
            padding: 10px 20px 10px 20px;
            text-decoration: none;
            vertical-align: middle;
        }

        .btn:hover {
            background: #3cb0fd;
            background-image: -webkit-linear-gradient(top, #3cb0fd, #3498db);
            background-image: -moz-linear-gradient(top, #3cb0fd, #3498db);
            background-image: -ms-linear-gradient(top, #3cb0fd, #3498db);
            background-image: -o-linear-gradient(top, #3cb0fd, #3498db);
            background-image: linear-gradient(to bottom, #3cb0fd, #3498db);
            text-decoration: none;
            vertical-align: middle;
        }
        
        .sep {
            background-color: black;
            color: white;
            width: 10px;
        }
    </style>
    <SCRIPT Language="javascript">
        function refreshTimer() {
            var ele = document.getElementById("fullState");
            if(ele.style.display == "block") {
                // don't refresh
            }
            else {
                // refresh
                document.location.reload(true);
            }
        }

        function post(path, params, method) {
            method = method || "post"; // Set method to post by default if not specified.

            // The rest of this code assumes you are not using a library.
            // It can be made less wordy if you use one.
            var form = document.createElement("form");
            form.setAttribute("method", method);
            form.setAttribute("action", path);

            for(var key in params) {
                if(params.hasOwnProperty(key)) {
                    var hiddenField = document.createElement("input");
                    hiddenField.setAttribute("type", "hidden");
                    hiddenField.setAttribute("name", key);
                    hiddenField.setAttribute("value", params[key]);

                    form.appendChild(hiddenField);
                }
            }

            document.body.appendChild(form);
            form.submit();
        }

        function setNext(index) {
            post('/setnext/', {id: index});
        }

        function updateParameters(taskId) {
            let node = document.getElementById('parameters');
            post('/updateparameters/', {taskId: taskId, parameters: node.textContent});
        }

        function toggleShowState() {
            var ele = document.getElementById("fullState");
            if(ele.style.display == "block") {
                ele.style.display = "none";
            }
            else {
                ele.style.display = "block";
            }
        }

        window.onload = function() { 
            setInterval(refreshTimer, 3000);
        };

    </SCRIPT>
    <div id="wrapper">
        <div id="header">
            <div style="text-align:center; padding-top:30; margin-bottom:30; width=100%;">
                <a class="btn" onclick="updateParameters(${nextTask})" title="Save Changes and Continue Execution">&#9658</a>
                <a class="btn" onclick="updateParameters(-100)" title="Stop Debugging and Continue Execution">&#9724</a>
                <a class="btn" onclick="toggleShowState()" title="Show/Hide the latest tasks and inputs">&#916</a>
            </div>
        <table width="100%">
            <tr>
                <td id="start" width="25px"><div style="position: relative; background-color: #00FF00; width: 25;  height: 25;  border-radius: 50%;"></div></td>
                <td class="sep">&rarr;</td>`;
    html += htmlTasks;

    let fullStateJson = JSON.stringify(state, null, 4);
    let parametersHeader = nextTaskInstance ? nextTaskInstance.name + ' Parameters (editable json)': 'No task selected to run next';
    let parametersJson = nextTaskInstance ? JSON.stringify(nextTaskInstance.parameters, undefined, 4) : '';
    let parametersEncoded = "'" + encodeURIComponent(parametersJson) + "'";

    let htmlEnd = `<td id="end" width="25px" ><div style="position: relative; background-color: #FF0000; width: 25;  height: 25;  border-radius: 50%;"></div></td>
                <td id="spacer"><div></div></td>
            </tr>
        </table>
        <div id="fullState" style="display:none; width:100%; margin-top:30; white-space: pre-wrap;">${fullStateJson}</div>
        <div style="width:100%; margin-top:30; text-align:center;">${parametersHeader}</div>
        <div contenteditable id="parameters" style="background-color:#D0D0D0; color:#000000; white-space: pre-wrap;">${parametersJson}</div>
        </div>
        <div id="content">
            <div style="text-align:center; width:100%">Console Output</div>
            <div id="console" style="white-space: pre-wrap; height=">
            ${consoleOutput}
            </div>
        </div>
    </div>
    </html>`;

    html += htmlEnd;

    return html;
}

function getNext() {
    if (resumeExecution && state && state.tasks) {
        return { next: nextTaskToExecute, parameters: state.tasks[nextTaskToExecute].parameters }
    }
    return { next: -1, parameters: {} }
}