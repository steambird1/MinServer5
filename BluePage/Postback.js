var mins_timers = {};

// function mins_postback(info,para)
// info: The function will be called (-> .field)
// para: The external parameter (-> .parameter)

function mins_remove_cr(str) {
    return str.replace(/\r/g,"");
}

function mins_destrify(proceed_str) {
    //return proceed_str.replace(/\^n/g,"\n").replace(/\^d/g,".").replace(/\^c/g,",").replace(/\^e/g,"=").replace(/\^s/g," ").replace(/\^\^/g,"^");
    var result = "";
    for (let i = 0; i < proceed_str.length; i++) {
        let cur = proceed_str[i];
        switch (cur) {
            case '^':
                switch (proceed_str[i + 1]) {
                    case '^':
                        result += '^';
                        break;
                    case 'n':
                        result += '\n';
                        break;
                    case 'd':
                        result += '.';
                        break;
                    case 'c':
                        result += ',';
                        break;
                    case 'e':
                        result += '=';
                        break;
                    case 's':
                        result += ' ';
                        break;
                    default:
                        result += cur + proceed_str[i + 1]; // No processor
                }
                i++;
                break;
            default:
                result += cur;
        }
    }
    return result;
}

function mins_format(str) {
    return str.replace(/\\/g, '\\\\').replace(/"/g, '\\"');   
}

function mins_dealing(content) {
    var spls = content.split("\n");
    for (let i = 0; i < spls.length; i++) {
        try {
            var spld = spls[i].trimLeft();
            var orderer = spld.split(" ", 2);
            if (orderer.length < 2) continue;
            var curs = orderer[1].split("=", 2);
            switch (orderer[0]) {
                case "@control":
                    var left_name = curs[0].split(".", 2);
                    document.getElementById(left_name[0])[left_name[1]] = mins_destrify(curs[1]);
                    break;
                case "@job":
                    window[curs[0]](mins_destrify(curs[1]));
                    break;
                case "@timer":
                    var data = curs[1].split(",", 3);
                    mins_timers[curs[0]] = setInterval(function () {
                        mins_postback(data[1].trim(), mins_destrify(mins_remove_cr(data[2])));
                    }, parseInt(data[0]));
                    break;
                case "@timer_remove":
                    clearInterval(mins_timers[curs[0]]);
                    break;
            }
        } catch (err) {
            console.log("MinServer Postback Service: " + err.toString())
        }
    }
}

// These functions are for BluePage jobs:
// The parameter must be one string ('data').
function mins_write(data) {
    document.write(data);
}

function mins_alert(data) {
    alert(data);
}

// Format: [Target BlueBetter Function]:[Confirm information], will call the function later
function mins_confirm(data) {
    var exp = data.indexOf(":");
    var target = data.substr(0, exp);
    var info = data.substr(exp + 1);
    mins_postback(target.trim(), Number(confirm(info)));
}

function mins_prompt(data) {
    var exp = data.indexOf(":");
    var target = data.substr(0, exp);
    var info = data.substr(exp + 1);
    mins_postback(target.trim(), prompt(info));
}

function mins_panic(data) {
    document.write("<h1>Postback Error</h1><br /><p>The page has experienced a fatal PostBack error. Details:</p><p>" + data + "</p><p>Contact the owner of website for help.");
}

function mins_refresh(data) {
    location.reload();
}