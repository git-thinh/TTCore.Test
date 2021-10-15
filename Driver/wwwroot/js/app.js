var __app, __files = [];
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
                $('#menu').addClass('hide');
                $('#item-setting').addClass('hide');
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
function __upload(visible, callback) {
    __app.upload.callback = callback;
    if (visible == false) {
        $('#upload').addClass('hide');
    } else {
        //var folderId = __app.currentFolder.id;
        //if (folderId == null || folderId.length == 0) {
        //    $('#upload').addClass('hide');
        //    __app.alert.message = 'Please choose folder to upload files';
        //    __alert(true);
        //    return;
        //}
        $('#upload input[type="file"]').val('');
        $('#upload').removeClass('hide');
    }
}
function __edit(visible) {
    if (visible == false) {
        $('#edit-menu').addClass('hide');
        $('#edit').addClass('hide');
    } else {
        $('#edit-menu').addClass('hide');
        $('#edit').removeClass('hide');
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
    //setTimeout(function () { __app.item_DeleteAlert(); }, _timeOut);
    setTimeout(function () { __edit(true); $('#edit-menu').removeClass('hide'); __app.item_lineFormat(); }, _timeOut);
    setTimeout(function () { __app.line_Click(2); }, _timeOut + 500);

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
                    error: '',
                    callback: null
                },

                item: {
                    allow_edit: true,

                    indexs: [],
                    new_ids: [],
                    setting_id: -1,

                    id: '',
                    font_size: 25,
                    line_height: 36,
                    text: _dataText,
                    lines: []
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
            file_listAll: function () {
                var _self = this;
                var items = _.filter(_self.all, o => o.is_dir != true);
                _self.items = items;
                console.log('file_listAll files = ', items);

            },
            file_uploadFiles: function (files) {
                if (typeof this.upload.callback == 'function')
                    this.upload.callback(files);
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
            menu_itemClick: function (code) {
                $('#edit-menu').addClass('hide');

                switch (code) {
                    case 'BOOKMARK':
                        this.item_updateBookmark();
                        break;
                    case 'UPDATE_CONTENT':
                        this.item_updateContent();
                        break;
                    case 'INSERT_LINE':
                        this.line_insertNewBlank();
                        break;
                    case 'DELETE_SELECT':
                        this.line_deleteSelection();
                        break;
                    case 'INSERT_IMAGE':
                        this.line_insertImage();
                        break;
                    case 'INSERT_MEDIA':
                        this.line_insertMedia();
                        break;
                    case 'INSERT_YOUTUBE':
                        this.line_insertYoutube();
                        break;
                }
            },

            item_lineFormat: function () {
                var _self = this,
                    item = _self.item, indexs = item.indexs,
                    text = item.text || '';
                var a = text.split('\n');
                a = _.filter(a, o => o.trim().length > 0);
                //console.log(a);
                item.lines = a;
            },
            item_updateBookmark: function () { },
            item_updateContent: function () { },

            line_Click: function (index) {
                var _self = this, item = _self.item, indexs = item.indexs;
                if (!item.allow_edit) return;

                var ix = _.findIndex(indexs, o => o == index);
                if (ix != -1) {
                    indexs.splice(ix, 1);
                } else {
                    indexs.push(index);
                }

                if (item.allow_edit) $('#edit-content').addClass('mode-edit');
                else $('#edit-content').removeClass('mode-edit');

                $('#line-' + index).toggleClass('line-selection');
                console.log(index, indexs);
            },
            line_settingClick: function (index, event) {
                this.item.setting_id = index;
                $('#edit-menu').addClass('hide');
                $('#item-setting').toggleClass('hide');
                event.stopPropagation();
            },
            line_newClick: function (id) {
                var _self = this, new_ids = _self.item.new_ids;
                console.log(id);
                $('#' + id).toggleClass('bg-info');
            },
            line_insertNewBlank: function () { },
            line_deleteSelection: function () { },
            line_insertImageDisplay: function (imageBase64) {
                var _self = this, item = _self.item, indexs = item.indexs;
                if (indexs.length == 0) return;
                var ix = indexs[indexs.length - 1];
                var target = document.getElementById('line-' + ix);
                if (target) {
                    var el = document.createElement('p');
                    var id = new Date().getTime();
                    var s = `<img class="w-100" src="` + imageBase64 + `"/>
                        <i onclick="__app.line_settingClick(` + id + `,event)" class="bi-gear fs-4 text-danger position-absolute end-0 bottom-0 me-2"></i>`;

                    el.setAttribute('id', id);
                    el.setAttribute('class', 'line-new text-center p-1 px-5 position-relative');
                    el.setAttribute('onclick', '__app.line_newClick(' + id + ')');
                    el.innerHTML = s;
                    target.parentNode.insertBefore(el, target);
                }
            },
            line_insertImageSubmit: function () {
                var _self = this;
                console.log('line_insertImageSubmit files = ', __files);
                if (__files == null || __files.length == 0) return;

                __upload(false);

                var file = __files[0];
                var reader = new FileReader();
                reader.onload = function (e) {
                    var imageBase64 = e.target.result;
                    //img.src = data;
                    //console.log(data);
                    _self.line_insertImageDisplay(imageBase64);
                };
                reader.readAsDataURL(file);

                ////if (files == null || files.length == 0) return;
                ////var formData = new FormData();
                ////for (var i = 0; i < files.length; i++) {
                ////    formData.append(files[i].name, files[i]);
                ////}

                ////$.ajax({
                ////    url: '/api/driver/upload',
                ////    type: 'POST',
                ////    data: formData,
                ////    processData: false,
                ////    contentType: false,
                ////    success: function (message) {
                ////        console.log('file_uploadFiles = ', message);
                ////        _self.app_loadAllItems();
                ////        _self.file_listAll();
                ////    },
                ////    error: function () {
                ////        alert("there was error uploading files!");
                ////    }
                ////});

                //_self.upload.error = 'Please enter name of folder';
            },
            line_insertImage: function () {
                var _self = this, item = _self.item, indexs = item.indexs;
                if (indexs.length == 0) {
                    _self.alert.message = 'Please line selection before';
                    __alert(true);
                } else {
                    __upload(true, this.line_insertImageSubmit);
                }
            },
            line_insertMedia: function () { },
            line_insertYoutube: function () { }
        }
    });
}

var _dataText = `THANH TỊNH KINH

太 上 老 君 说 常 清 静 经

THÁI THƯỢNG LÃO QUÂN THUYẾT THƯỜNG THANH TỊNH KINH

大道无形，生育天地；大道无情，运行日月；大道无名，长养万物；吾不知其名强名曰道。夫道者 : 有清有浊，有动有静；天清地浊，天动地静；男清女浊，男动女静；降本流末，而生万物。清者浊之源，动者静之基；人能常清静，天地悉皆归。

Đại đạo vô hình, sanh dục thiên địa; đại đạo vô tình, vận hành nhật nguyệt; đại đạo vô danh, trường dưỡng vạn vật; ta chẳng biết gọi là gì, chỉ tạm gọi là đạo. Phàm Đạo ấy : có  thanh có  trọc, có  động có tĩnh; thiên thanh địa trọc, thiên động địa tĩnh; nam thanh nữ trọc, nam động nữ tĩnh; giáng gốc giữ ngọn, mà sanh vạn vật. Thanh là nguồn của trọc, động  là cơ của tĩnh; người thường hay thanh tĩnh, thì Đạo trời đất đều gồm đủ nơi thân.

夫人神好清，而心扰之；人心好静，而欲牵之。常能遣其欲，而心自静；澄其心，而神自清；自然六欲不 生，三毒消灭。所以不能者，为心未澄，欲未遣也，能遣之者 : 内观其心，心无其心；外观其形，形无其形；远观其物，物无其物；三者既无，唯见于空。观空亦空，空无所空；所空既无，无无亦无；无无既无，湛然常寂。寂无 所寂，欲岂能生；欲既不生，即是真静。真常应物，真常得性；常应常静，常清静矣。如此清静，渐入真道；既入真道，名为得道；虽名得道，实无所得；为化众 生，名为得道；能悟之者，可传圣道。

Phàm thần của người ưa thanh, mà tâm thường bị quấy rối; tâm của người ưa tĩnh, mà bị dục kéo lôi. Thường chế ngự được dục, thì tâm tự tĩnh; lắng được tâm, thì thần tự thanh; tự nhiên lục dục chẳng sanh, tam độc tiêu diệt. Chưa được như thế, vì tâm chưa lắng, dục chưa chế ngự vậy. Phải thường chế ngự : trong xem xét tâm, tâm không thật có gì để gọi là tâm; ngoài xem xét thân, thân không thật có gì để gọi là thân; xa xem xét vật, vật không thật có gì để gọi là vật; Cả 3 đều không, mà còn cái thấy cả 3 đều không. Cái thấy là không cũng không, không không chỗ không; chỗ không đã không, không không cũng không; không không đã không, trạm nhiên thường tịch. Tịch không chỗ tịch, chẳng sinh khởi dục; dục đã chẳng sanh, tức là chân tĩnh. Chân thường ứng vật, chân thường được tính; thường ứng thường tĩnh, thường thanh tĩnh vậy. Thanh tĩnh như thế, dần dần nhập chân đạo; đã nhập chân đạo, gọi là đắc đạo; tuy gọi đắc đạo, thật không chỗ được; vì dạy chúng sanh, tạm gọi đắc đạo; ngộ được như vậy thì có thể truyền thánh đạo .

上士无争，下士好争。上德不德，下德执德，执着之者，不明道德。众生所以不得真道者，为有妄心，既有妄心，即惊其神，既惊其神，即着万物，既着万物，即生 贪求，既生贪求，即是烦恼，烦恼妄想，忧苦身心，便遭浊辱，流浪生死，常沉苦海，永失真道。真常之道，悟者自得；得悟道者，常清静矣！

Thượng sĩ chẳng tranh, hạ sĩ hay tranh. Bậc thượng đức không để ý đến kẻ khác coi mình là có đức hay chê mình là không có đức, còn bậc hạ đức chấp đức, do vì bám chấp, nên  đạo đức chẳng trong sáng. Chúng sanh sở dĩ chẳng được chân đạo bởi vì có vọng tâm, đã có vọng tâm, thì kinh động đến thần, đã kinh động đến thần, tức là bám chấp vạn vật, đã chấp vạn vật, thì sanh tham cầu, đã sanh tham cầu, chính là phiền não, phiền não vọng tưởng làm ưu khổ thân tâm, tạo ra vinh nhục đổi dời, nổi trôi sanh tử, đắm chìm bể khổ, mất hết chân đạo. Đạo chân thường này, ngộ mà tự được; ngộ được đạo thì thường thanh tĩnh vậy.

——————————————————————

仙人葛翁曰 : 吾得真道，曾诵此经万遍。此经是天人所习，不传下士。吾昔受之于东华帝君，东华帝君受之于金阙帝君，金阙帝君受之于西王母。西王一线乃口口相传，不记文 字。吾今于世，书而录之。上士悟之，升为天仙；中士修之，南宫列官；下士得之，在世长年。游行三界，升入金门。

Tiên nhân Cát ông nói  : Ta được chân đạo, thường tụng kinh này vạn lần. Kinh này là chỗ thiên nhân góp lại chẳng truyền cho kẻ hạ sĩ. Ta nhận được từ Đông Hoa Đế quân, Đông Hoa Đế Quân nhận  từ Kim Khuyết Đế Quân, Kim Khuyết Đế Quân nhận từ Tây Vương Mẫu. Tây Vương chỉ theo một cách là khẩu khẩu tương truyền, chẳng ghi văn tự, ta nay ghi lại thành sách cho đời. Kẻ Thượng sĩ gặp được sẽ biết đường để thăng lên thiên tiên; trung sĩ tu được có thể đến bậc liệt quan ở nam cung; hạ sĩ học được cũng được sống lâu trên đời. Du hành ba cõi, lên đến kim môn.

左玄真人曰 : 学道之士，持诵此经者，即得十天善神，拥护其身。然后玉符保神，金液炼形。形神俱妙，与道合真。

Tả Huyền chân nhân nói  : người học đạo trì tụng kinh này thì được thiện thần ở 10 cõi trời ủng hộ thân mình, mà về sau được ngọc phù bảo thần, kim dịch luyện hình. Hình thần đều diệu, cùng đạo hợp chân .

正一真人曰 : 人家有此经，悟解之者，灾障不干，众圣护门。神升上界，朝拜高真。功满德就，相感帝君。诵持不退，身腾紫云。

Chánh Nhất chân nhân nói : nhà nào có kinh này, ngộ giải được thì tai chướng chẳng ngại, chúng thánh bảo vệ ngoài cửa. Thần thăng thượng giới, chào hỏi cao chân. Công mãn đức tựu, tương cảm đế quân. Đọc giữ chẳng ngừng, mây tím sẽ rước thân này bay lên.
`


var a = [],
    es = document.querySelectorAll('a');
for (var i = 0; i < es.length; i++) {
    var l = es[i].getAttribute('href');
    if (l && l.indexOf('/jobs/job-opening/') != -1)
        a.push(l.split('?')[0]);
}; a.join('^');