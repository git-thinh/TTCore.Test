var __app;

$(document).ready(__init);
function __init() {
    __app = new Vue({
        el: '#app',
        data: function () {
            return {
                groups: [],
                tags: [],
                types: [],
                files: [],
                folders: []
            };
        },
        created: function () {
            var _self = this;
            setTimeout(_self.app_loadDataInit, 1);
        },
        mounted: function () {
            var _self = this;
            if (localStorage['folders'] != null) {
                var folders = JSON.parse(localStorage['folders']);
                var files = JSON.parse(localStorage['files']);
                console.log(folders, files);
                _self.folders = folders;
                _self.files = files;
                _self.app_Ready();
            } else {
                var time = new Date().getTime();
                var ps = [
                    fetch('/api/driver/folders?_=' + time).then(r => r.json()),
                    fetch('/api/driver/files?_=' + time).then(r => r.json())
                ];
                Promise.all(ps).then(a => {
                    console.log(a);
                    _self.folders = a[0];
                    _self.files = a[1];
                    localStorage['folders'] = JSON.stringify(a[0]);
                    localStorage['files'] = JSON.stringify(a[1]);
                    _self.app_Ready();
                });
            }
        },
        methods: {
            toKB: function (size) {
                return (size / 1024).toString().split('.')[0];
            },
            app_loadDataInit: function () {
                var _self = this;
                var time = new Date().getTime();
                var ps = [
                    fetch('/data/group.json?_=' + time).then(r => r.json()),
                    fetch('/data/tag.json?_=' + time).then(r => r.json()),
                    fetch('/data/type.json?_=' + time).then(r => r.json())
                ];
                Promise.all(ps).then(a => {
                    console.log(a);
                    _self.groups = a[0];
                    _self.tags = a[1];
                    _self.types = a[2];
                });
            },
            app_Ready: function () {
                var _self = this;
                Vue.nextTick(function () {
                    _self.app_setupUI();
                });
            },
            app_setupUI: function () {
                var _self = this;
                $('#loading').addClass('hide');
                $('#header').removeClass('hide');
                $('#body').removeClass('hide');
            }
        }
    });
}

function notifyInit() {

    var connection = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/image")
        .withHubProtocol(new signalR.protocols.msgpack.MessagePackHubProtocol())
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on("RAW_FILE", function (m) {
        console.log(m);
        __app.items = [];
        __app.file = m.FileName;
        console.clear();
    });
    connection.on("RAW_PROCESS", function (m) {
        console.log(m);
        __app.total = m.total;
        __app.items.push(m);
    });
    connection.on("RAW_DONE", function (m) {
        console.log(m);
        __app.queue = m.Queue;
        //alert('DONE: ' + m.File);
    });
    connection.on("RAW_ERROR", function (m) {
        //console.log(m);
        console.error('ERROR: ' + m);
    });

    connection.start()
        .then(function () { console.log('SignalR Started...'); })
        .catch(function (err) { return console.error(err); });
}