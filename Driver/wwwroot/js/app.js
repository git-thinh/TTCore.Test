var __app;
$(document).ready(__domReady);
function __domReady() {
    var time = new Date().getTime();
    fetch('/api/file/files?dir=ui&_=' + time).then(r => r.json()).then(files => {
        var time = new Date().getTime();
        var urls = _.map(files, name => fetch('/ui/' + name + '?_=' + time).then(r => r.text()));
        Promise.all(urls).then(a => {
            //console.log(a);
            var app = document.getElementById('app');
            for (var i = 0; i < a.length; i++) {
                const parser = new DOMParser();
                const doc = parser.parseFromString(a[i], "text/html");
                const el = doc.body.firstElementChild;
                //console.log(el);
                app.appendChild(el);
            }

            window.addEventListener("click", function (e) {
                if (e.target.getAttribute('id') == 'menu_toggle') return;
                $('#menu').addClass('hide');
            });

            __initVue();
        });
    });
}
function __loading(visible) {
    if (visible == false) $('#loading').addClass('hide');
    else $('#loading').removeClass('hide');
}
function __alert(visible, callback, showButton) {
    __app.alert.callback = callback;
    if (visible == false) {
        $('#alert').addClass('hide');
        __app.alert.callback = null;
        __app.alert.value = '';
        __app.alert.title = '';
        __app.alert.input = '';
        __app.alert.message = '';
        __app.alert.check_title = '';
    } else {
        $('#alert').removeClass('hide');
        __app.alert.button_show = showButton || false;
    }
}
function __upload(visible) {
    if (visible == false) $('#upload').addClass('hide');
    else {
        //var folderId = __app.currentFolder.id;
        //if (folderId == null || folderId.length == 0) {
        //    $('#upload').addClass('hide');
        //    __app.alert.message = 'Please choose folder to upload files';
        //    __alert(true);
        //    return;
        //}
        $('#upload').removeClass('hide');
    }
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

function __initVue() {
    var _timeOut = 700;
    //setTimeout(__upload, _timeOut);
    //setTimeout(function () { __app.file_listAll() }, _timeOut);
    //setTimeout(function () { __app.item_DeleteAlert() }, _timeOut);

    __app = new Vue({
        el: '#app',
        data: function () {
            return {
                back_show: false,
                alert: {
                    button_show: true,
                    title: '',
                    message: '',
                    input: '',
                    value: '',
                    check_title: '',
                    check_value: true,
                    error: '',
                    callback: null
                },
                upload: {
                    message: '',
                    files: '',
                    error: ''
                },

                root: {},
                currentFolder: {},

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

                _self.all = all;

                if (localStorage['folder'] == null) localStorage['folder'] = '{}';
                var folder = JSON.parse(localStorage['folder']);
                _self.currentFolder = folder;

                var parentId = folder.id;
                if (parentId == null) parentId = rootId;
                else _self.back_show = true;
                var items = _.filter(_self.all, o => o.is_dir && o.parents != null && _.findIndex(o.parents, z => z == parentId) != -1);
                _self.items = items;

                _self.app_Ready();
            } else _self.app_loadAllItems();
        },
        methods: {
            toKB: function (size) {
                return (size / 1024).toString().split('.')[0];
            },
            search: function () {
                var _self = this;
                $('#search').toggleClass('hide');
            },
            alert_Ok: function () {
                var _self = this, callback = _self.alert.callback;
                if (typeof callback == 'function') callback();
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

                    localStorage['root'] = rootId;

                    _self.all = a[1];
                    localStorage['all'] = JSON.stringify(a[1]);

                    if (localStorage['folder'] == null) localStorage['folder'] = '{}';
                    var folder = JSON.parse(localStorage['folder']);
                    _self.currentFolder = folder;

                    var parentId = folder.id;
                    if (parentId == null) parentId = rootId;
                    else _self.back_show = true;
                    var items = _.filter(_self.all, o => o.is_dir && o.parents != null && _.findIndex(o.parents, z => z == parentId) != -1);
                    _self.items = items;
                    //console.log('mounted items = ', items);

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
            item_Click: function (item) {
                var _self = this, items = [];
                console.log('CLICK item = ', item);
                if (item.is_root) {
                    _self.back_show = true;
                    items = _.filter(_self.all, o => o.parents != null
                        && _.findIndex(o.parents, z => z == item.id) != -1);
                    _self.items = items;

                    _self.currentFolder = item;
                    localStorage['folder'] = JSON.stringify(item);
                    return;
                } else {
                    if (item.is_dir) {
                        _self.back_show = true;
                        items = _.filter(_self.all, o => o.parents != null
                            && _.findIndex(o.parents, z => z == item.id) != -1);
                        _self.items = items;

                        item.back_id = _self.currentFolder.id;
                        _self.currentFolder = item;
                        localStorage['folder'] = JSON.stringify(item);
                    }
                }
            },
            back_Click: function () {
                var _self = this, items = [], item = _self.currentFolder;
                //console.log('BACK item = ', item);

                var back = _.find(_self.all, o => o.id == item.back_id);
                if (back == null) {
                    _self.back_show = false;
                    back = {};
                } else _self.back_show = true;
                _self.currentFolder = back;
                localStorage['folder'] = JSON.stringify(back);

                items = _.filter(_self.all, o => o.is_dir && o.parents != null
                    && _.findIndex(o.parents, z => z == item.back_id) != -1);
                _self.items = items;
            },
            folder_createNewSubmit: function () {
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

                var parentId = '';
                if (_self.currentFolder.id != null)
                    parentId = _self.currentFolder.id;
                fetch('/api/driver/folder/create/' + value + '?parentFolderId=' + parentId).then(r => r.json()).then(v => {
                    console.log('item_createNew = ', value, parentId, v);
                    _self.app_loadAllItems();
                });
            },
            folder_createNew: function () {
                var _self = this;
                _self.alert.title = 'Create New Folder';
                _self.alert.input = 'Please enter name of folder';
                __alert(true, _self.folder_createNewSubmit, true);
            },
            item_DeleteSubmit: function () {
                var _self = this, folder = _self.currentFolder, isDeleteChilds = _self.alert.check_value;
                    __alert(false);
                __loading();

                var fs = [];
                if (isDeleteChilds) {
                    var childs = _.filter(_self.all, o => o.is_dir && o.parents != null
                        && _.findIndex(o.parents, z => z == folder.id) != -1);

                    for (var i = 0; i < childs.length; i++) {
                        var f = fetch('/api/driver/delete/' + childs[i].id).then(r => r.json());
                        fs.push(f);
                    }
                }
                var d = fetch('/api/driver/delete/' + folder.id).then(r => r.json());
                fs.push(d);

                Promise.all(fs).then(a => {
                    //console.log(folder.id, delChilds, childs, a);
                    _self.back_Click();
                    _self.app_loadAllItems();
                })
            },
            item_DeleteAlert: function () {
                var _self = this, folder = _self.currentFolder;
                if (folder.id == null) return;
                _self.alert.title = 'Delete Folder';
                _self.alert.message = 'Are you sure want to delete folder: ' + folder.name;
                _self.alert.check_title = 'Delete all subfolders and files';
                __alert(true, _self.item_DeleteSubmit, true);
            },
            file_listAll: function () {
                var _self = this;
                var items = _.filter(_self.all, o => o.is_dir != true);
                _self.items = items;
                console.log('file_listAll files = ', items);

            },
            file_uploadFiles: function (files) {
                var _self = this;
                console.log('file_uploadFiles files = ', files);

                if (files == null || files.length == 0) return;
                var formData = new FormData();
                for (var i = 0; i < files.length; i++) {
                    formData.append(files[i].name, files[i]);
                }

                $.ajax({
                    url: '/api/driver/upload',
                    type: 'POST',
                    data: formData,
                    processData: false,
                    contentType: false,
                    success: function (message) {
                        console.log('file_uploadFiles = ', message);
                        _self.app_loadAllItems();
                        _self.file_listAll();
                    },
                    error: function () {
                        alert("there was error uploading files!");
                    }
                });

                //_self.upload.error = 'Please enter name of folder';
            }
        }
    });
}
