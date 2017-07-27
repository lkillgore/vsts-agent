var path = require('path');
var express = require('express');
var jsdom = require('jsdom');

var app = express();
app.set('port', 7777);
app.use(express.logger('dev'));  /* 'default', 'short', 'tiny', 'dev' */
app.use(express.bodyParser());

var state = {
    tasks: [
        { name: "Loading...", parameters: { key1: "value1", key2: "value2" } }
    ],
    current: 0
}
var nextTaskToExecute = 0
var consoleLines = [''];
var resumeExecution = false;

app.get('/', function (req, res) {
   res.send(getMainView());
})

app.post('/update', function (req, res) {
    resumeExecution = false;
    nextTaskToExecute = 0;
    state = req.body;
    res.status(200);
    res.end();
})

app.post('/updateparameters', function (req, res) {
    resumeExecution = true;
    let taskId = parseInt(req.body.taskId);
    let parameters = JSON.parse(req.body.parameters);
    if (state && state.tasks) {
        state.tasks[taskId].parameters = parameters;
    }
    res.redirect('/');
})

app.post('/setnext', function (req, res) {
    nextTaskToExecute = parseInt(req.body.id);
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

app.listen(7777);

function getMainView() {
    let consoleOutput = consoleLines.join('\n');
    let html = `<html>
    <SCRIPT Language="javascript">
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
    </SCRIPT>

    <table width="100%">
        <tr>
            <td id="start" width="25px"><div style="position: relative; background-color: #00FF00; width: 25;  height: 25;  border-radius: 50%;"></div></td>
            <td id="separator1" width="10px">&rarr;</td>`;

    let nextTask = undefined;
    let nextTaskInstance = undefined;
    if (state && state.tasks) {
        nextTask = nextTaskToExecute >= 0 ? nextTaskToExecute : state.current + 1;
        let width = 100 / (state.tasks.length + 2);
        for(let i = 0; i < state.tasks.length; i++) {
            let name = state.tasks[i].name;
            let color = state.current === i ? 'blue' : 'darkblue';
            let radioColor = nextTask === i ? 'green' : 'gray';
            let hint = nextTask === i ? 'This is the next task that will execute when you hit continue':'Click here to make this the next task that will execute';
            let onclick = nextTask === i ? '' : `onclick="setNext(${i})"`
            html += `<td style="color:#FFFFFF;background:${color}" width="${width}%" height="75px">`;
            if (i <= state.current + 1) {
                html += `<div ${onclick} style="vertical-align:top; float:right; background-color:${radioColor}; width: 15;  height: 15;  border-radius: 50%;" title="${hint}"></div>`;
            }
            html += `<div align="center">${name}</div></td><td width="10px">&rarr;</td>`;
            if (i === nextTask) {
                nextTaskInstance = state.tasks[i];
            }
        }
    }

    let parametersHeader = nextTaskInstance ? nextTaskInstance.name + ' Parameters (editable json)': 'No task selected to run next';
    let parametersJson = nextTaskInstance ? JSON.stringify(nextTaskInstance.parameters, undefined, 4) : '';
    let parametersEncoded = "'" + encodeURIComponent(parametersJson) + "'";

    let htmlEnd = `<td id="end" width="25px" ><div style="position: relative; background-color: #FF0000; width: 25;  height: 25;  border-radius: 50%;"></div></td>
            <td id="spacer"><div></div></td>
        </tr>
    </table>
    <div><button type="button" onclick="updateParameters(${nextTask})">Save Changes and Continue Execution</button></div>
    <div width="100%">${parametersHeader}</div>
    <div contenteditable id="parameters" style="background-color:#D0D0D0; color:#000000; white-space: pre-wrap;">${parametersJson}</div>
    <div>Console Output</div>
    <div id="console" style="background-color:#000000; color:#D0D0D0; white-space: pre-wrap;">
    ${consoleOutput}
    </div>
    </html>`;

    html += htmlEnd;

    return html;
}

function getNext() {
    if (resumeExecution && state && state.tasks && nextTaskToExecute && state.tasks.length < nextTaskToExecute) {
        return { next: nextTaskToExecute, parameters: state.tasks[nextTaskToExecute].parameters }
    }
    return { next: -1, parameters: {} }
}