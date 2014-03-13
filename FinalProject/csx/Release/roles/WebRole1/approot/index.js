$(function () {
    $("#searchbar").on("input", loadSuggestions);
});

function loadSuggestions() {
    var data = $("#searchbar").val();

    $.ajax({
        url: "http://54.201.96.116/NBAStats1/index.php?name=" + escape(data)
        // JSONP is dangerous, domain policy is safer
    }).done(function (html) {
        getNBA(html);
    });

    $.ajax({
        url: "queries.asmx/query?data= " + data,
    }).done(function(response){
        displayResults(response);
    });
}

function getNBA(json) {
    $("#nbaresults").empty();
    if (json != "[]") {
        var table = $('<table class="tableresults"></table>');
        var display = "";

        // Header
        display += "<tr>";
        display += "<th>Player Name</td>";
        display += "<th>GP</td>";
        display += "<th>FGP</td>";
        display += "<th>TPP</td>";
        display += "<th>FTP</td>";
        display += "<th>PPG</td>";
        display += "</tr>";

        json = JSON.parse(json);
        for (var i = 0; i < json.length; i++) {
            display += "<tr>";
            display += "<td>" + json[i].PlayerName + "</td>";
            display += "<td>" + json[i].GP + "</td>";
            display += "<td>" + json[i].FGP + "</td>";
            display += "<td>" + json[i].TPP + "</td>";
            display += "<td>" + json[i].FTP + "</td>";
            display += "<td>" + json[i].PPG + "</td>";
            display += "</tr>";
        }

        table.html(display);
        $("#nbaresults").append(table);
    }
}

function displayResults(json) {
    $("#results").empty();
    if (json != "Term not found.") {
        var items = JSON.parse(json);

        for (var i in items) {
            var link = $("<a></a>").html(items[i]);
            link.attr("href", i);

            var result = $("<p></p>").append(link);
            $("#results").append(result);
        }
    } else {
        var result = $("<p></p>").html("Term not found.");
        $("#results").append(result);
    }
}