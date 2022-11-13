var mins_timers = {};

/*
^n LF
^^ ^
^d .
^c ,
^e =
^s SPACE
*/
function mins_destrify(proceed_str) {
    return proceed_str.replaceAll("^n","\n").replaceAll("^^","^").replaceAll("^d",".").replaceAll("^c",",").replaceAll("^e","=").replaceAll("^s"," ");
}

function mins_panic(data) {
    document.write("<h1>Postback Error</h1><br /><p>The page has experienced a fatal PostBack error. Details:</p><p>"+data+"</p><p>Contact the owner of website for help.");   
}

function mins_dealing(content) {
    try {
        var spls = content.split("\n");
        for (let i = 0; i < spls.length; i++) {
            var orderer = spls[i].split(" ", 2);
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
                    var data = curs[1].split(",", 2);
                    mins_timers[curs[0]] = setInterval(function() {
                        mins_postback(data[1].trim());
                    }, parseInt(data[0]));
                    break;
                case "@timer_remove":
                    clearInterval(mins_timers[curs[0]]);
                    break;
                default:
                    mins_panic("Bad postback command: "+spls[i]);
            }
        }
    } catch (err) {
        // TypeError?
    }
}