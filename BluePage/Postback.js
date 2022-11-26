var mins_timers = {};

function mins_remove_cr(str) {
    return str.replace(/\r/g,"");
}

function mins_destrify(proceed_str) {
    return proceed_str.replace(/\^n/g,"\n").replace(/\^\^/g,"^").replace(/\^d/g,".").replace(/\^c/g,",").replace(/\^e/g,"=").replace(/\^s/g," ");
}

function mins_panic(data) {
    document.write("<h1>Postback Error</h1><br /><p>The page has experienced a fatal PostBack error. Details:</p><p>"+data+"</p><p>Contact the owner of website for help.");   
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

        }
    }
}