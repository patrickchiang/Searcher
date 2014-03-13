$(function () {
    heartbeat();
    var intervalHb = setInterval(heartbeat, 2000);

    $("#submitBtn").click(search);

    $(".start").click(start);
    $(".stop").click(stop);
});

function start() {
    $.ajax({
        url: "/Admin.asmx/StartCrawling?root=" + $("#root").val(),
    });
}

function stop() {
    $.ajax({
        url: "/Admin.asmx/StopCrawling",
    });
}

function search() {
    var url = $("#url").val();
    $.ajax({
        url: "/Admin.asmx/GetTitle?url=" + url,
    }).done(function (data) {
        $(".title").html("Title: " + data);
    });
}

function heartbeat() {
    $.ajax({
        url: "/Admin.asmx/GetState",
    }).done(function (data) {
        $(".status").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetCPU",
    }).done(function (data) {
        $(".cpu").html(data + "%");
    });

    $.ajax({
        url: "/Admin.asmx/GetRAM",
    }).done(function (data) {
        $(".ram").html(data + " MB");
    });

    $.ajax({
        url: "/Admin.asmx/GetCrawledQty",
    }).done(function (data) {
        $(".discovery").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetLastURLs?limit=10",
    }).done(function (data) {
        $(".last").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetQueueSize",
    }).done(function (data) {
        $(".queue").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetIndexSize",
    }).done(function (data) {
        $(".index").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetErrors",
    }).done(function (data) {
        $(".errors").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetLastDictionaryTitle",
    }).done(function (data) {
        $(".inserted").html(data);
    });

    $.ajax({
        url: "/Admin.asmx/GetTrieSize",
    }).done(function (data) {
        $(".trie").html(data);
    });

}