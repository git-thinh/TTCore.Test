var __app;

$(document).ready(__init);
function __init() {
    window.addEventListener("click", function (e) {
        if (e.target.getAttribute('id') == 'menu_toggle') return;
        $('#menu').addClass('hide');
    });

    __app = new Vue({
        el: '#app',
        data: function () {
            return {
                alert: {
                    title: '',
                    message: '',
                    input: '',
                    value: '',
                    error: '',
                    callback: null
                },

                root: {},
                currentFolder: {},
                currentFolderParentId: '',
                currentFolderBackId: '',
                groups: [],
                tags: [],
                types: [],

                all: [],
                items: []
            };
        },
        watch: {
            'alert.value': function (value) {
                this.alert.error = value.length > 0 ? '' : 'Please input value';
            }
        },
        created: function () {
            var _self = this;
            setTimeout(_self.app_loadDataInit, 1);
        },
        mounted: function () {
            var _self = this;
            if (localStorage['all'] != null) {
                var rootId = localStorage['root'];
                var all = JSON.parse(localStorage['all']);
                //console.log(rootId, all);

                _self.currentFolderParentId = rootId;
                _self.currentFolderBackId = rootId;

                _self.all = all;
                var fs = _.filter(_self.all, o => o.parents != null && _.findIndex(o.parents, z => z == rootId) != -1);
                _self.items = fs;
                //console.log('mounted items = ', fs);

                _self.app_Ready();
            } else _self.app_loadAllItems();
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
                    //console.log(a);
                    _self.groups = a[0];
                    _self.tags = a[1];
                    _self.types = a[2];
                });
            },
            app_loadAllItems: function () {
                var _self = this;
                var time = new Date().getTime();
                var ps = [
                    fetch('/api/driver/root?_=' + time).then(r => r.json()),
                    fetch('/api/driver/all?_=' + time).then(r => r.json())
                ];
                Promise.all(ps).then(a => {
                    console.log(a);
                    var rootId = a[0].id;
                    _self.root = a[0];
                    _self.currentFolderParentId = rootId;
                    _self.currentFolderBackId = rootId;
                    localStorage['root'] = rootId;

                    _self.all = a[1];
                    var fs = _.filter(_self.all, o => o.parents != null && _.findIndex(o.parents, z => z == rootId) != -1);
                    _self.items = fs;
                    //console.log('mounted items = ', fs);
                    localStorage['all'] = JSON.stringify(a[1]);

                    //console.log('mounted items = ', _self.items);
                    _self.app_Ready();
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
                $('#header').removeClass('hide');
                $('#body').removeClass('hide');
                __loading(false);
            },
            search: function () {
                var _self = this;
                $('#search').toggleClass('hide');
            },
            item_Click: function (item) {
                var _self = this, selectId = item.id, backId = item.back_id;
                console.log('CLICK item = ', selectId, backId);
                //if (item.is_dir) {
                //    _self.currentFolder = item;
                //    _self.currentFolderBackId = _self.currentFolderParentId;
                //    _self.currentFolderParentId = selectId;
                //    var fs = _.filter(_self.all, o => o.parents != null && _.findIndex(o.parents, z => z == selectId) != -1);
                //    console.log('CLICK childs = ', fs);
                //    _self.items = fs;
                //} else {
                //    ;
                //}
            },
            prev_Click: function () {
                //var _self = this, backId = _self.currentFolderBackId;
                //var fs = _.filter(_self.all, o => o.parents != null && _.findIndex(o.parents, z => z == backId) != -1);
                //_self.items = fs;

                //var ps = _.map(fs, 'parents');
                //var bs = _.union.apply(_, ps);
                //var newBackId = bs[0];
                //_self.currentFolderBackId = newBackId;

                //var item = _.find(_self.all, o => o.id == newBackId);
                //if (item == null) item = {};
                //_self.currentFolder = item;

                //console.log('CLICK prev: ', newBackId, item);
            },
            alert_Ok: function () {
                var _self = this, callback = _self.alert.callback;
                if (typeof callback == 'function') callback();
            },
            item_createNewSubmit: function () {
                var _self = this, value = _self.alert.value;

                if (value.length == 0) {
                    _self.alert.error = 'Please input value';
                    return;
                }

                var item = _.find(_self.all, o => o.name.toLowerCase() == value.toLowerCase());
                if (item) {
                    _self.alert.error = 'Item exist';
                    return;
                }

                __alert(false);
                __loading();
                fetch('/api/driver/folder/create/' + value).then(r => r.json()).then(v => {
                    //console.log('item_createNew = ', value, v);
                    _self.app_loadAllItems();                    
                });
            },
            item_createNew: function () {
                var _self = this, parentId = _self.currentFolderParentId;
                _self.alert.title = 'Create New Folder';
                _self.alert.input = 'Please enter name of folder';
                __alert(true, _self.item_createNewSubmit);
            }
        }
    });
}

function __loading(visible) {
    if (visible == false) $('#loading').addClass('hide');
    else $('#loading').removeClass('hide');
}
function __alert(visible, callback) {
    __app.alert.callback = callback;
    if (visible == false) $('#alert').addClass('hide');
    else $('#alert').removeClass('hide');
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