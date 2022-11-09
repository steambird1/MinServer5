function mins_dealing(content) {
    var spls = content.split("\n");
    for (let i = 0; i < spls.length; i++) {
        var curs = spls[i].split("=", 2);
        var left_name = curs[0].split(".", 2);
        document.getElementById(left_name[0])[left_name[1]] = curs[1];
    }
}
