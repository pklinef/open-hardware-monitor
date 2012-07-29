$(window).load(function () {
    var graph;
    var refreshTimer;

    //keeping track of the two datefields
    //so that we don't traverse the DOM tree every time
    var startDateEl = $("#rangeStart");
    var endDateEl = $("#rangeEnd");
    var statusEl = $("#status");

    var Peer = Backbone.Model.extend({
        defaults: {
            name: '',
            address: ''
        }
    });

    var Peers = Backbone.Collection.extend({
        model: Peer,
        url: '/peers.json'
    });

    var PeerView = Backbone.View.extend({
        template: _.template($('#tpl-peer').html()),
        render: function (eventName) {
            var html = this.template(this.model.toJSON());
            this.setElement($(html));
            return this;
        }
    });

    var PeerList = Backbone.View.extend({
        tagName: 'ul',
        className: 'dropdown-menu',
        initialize: function () {
            this.collection.bind("reset", this.render, this);
        },
        render: function (eventName) {
            this.$el.html('');

            if (eventName) {
                // Add the refresh option to the peers list
                var template = _.template($("#tpl-peer-divider").html(), {});
                this.$el.append(template);
                $("#refresh_peers").click(function (e) {
                    peerList.$el.html('');
                    statusEl.text("Fetching peer list ...");
                    peers.fetch();
                    return false;
                });
            }

            // Add peers
            this.collection.each(function (peer) {
                var peerview = new PeerView({ model: peer });
                var $li = peerview.render().$el;
                this.$el.append($li);
            }, this);

            // Add local machine
            if (coll && coll.models.length > 0) {
                var peerview = new PeerView({ model: new Peer({ name: coll.models[0].toJSON().text, address: window.location.host }) });
                this.$el.append(peerview.render().$el);
            }

            $('.dropdown-toggle').dropdown();
            statusEl.text("Peer list updated");
            return this;
        }
    });

    var Node = Backbone.Model.extend({
        defaults: {
            id: '',
            text: '',
            parent: '',
            type: '',
            imageURL: '',
            data: ''
        },
        initialize: function () {
            this.bind("change:data", this.parseData);
        },
        parseData: function (e) {
            //convert the first column to Date objects
            var newData = _.map(this.get("data"), function (row) { return [new Date(row[0]), row[1]]; });
            this.set({ "data": null }, { silent: true });
            this.set({ "data": newData }, { silent: true });
            this.updateChart();
            statusEl.text("Graph updated");
        },

        updateChart: function () {
            if (graph == null) {
                graph = new Dygraph(
                    document.getElementById("sensor"),
                    this.get("data"),
                    {
                        title: this.get("text"),
                        xlabel: "Date",
                        ylabel: this.get("type"),
                        labels: ["Date", this.get("text")],
                        labelsDivStyles: { 'textAlign': 'right' },
                        showRangeSelector: true,
                        colors: ['#0074CC'],
                        strokeWidth: 1
                    }
                );
            }
            else {
                graph.updateOptions({ 'file': this.get("data"),
                    'title': this.get("text"),
                    'labels': ["Date", this.get("text")],
                    'ylabel': this.get("type")
                });
            }
        }
    });

    var Tree = Backbone.Collection.extend({
        model: Node,
        url: '/sensors'
    });

    var NodeView = Backbone.View.extend({
        template: _.template($('#tpl-node').html()),
        events: {
            "change input[type=radio]": "plot"
        },
        render: function (eventName) {

            var html = this.template(this.model.toJSON());
            this.setElement($(html));
            return this;
        },
        plot: function () {
            this.model.set({ "data": null }, { silent: true });
            statusEl.text("Fetching graph data ...");
            this.model.fetch({ data: { start: startDateEl.val(), end: endDateEl.val()} });
            return;
        }
    });

    var TreeView = Backbone.View.extend({
        tagName: 'table',
        id: 'tree',
        initialize: function () {
            this.collection.bind("reset", this.render, this);
        },
        render: function (eventName) {
            this.$el.html();

            if (this.collection.length > 0) {
                var computerName = this.collection.models[0].toJSON().text;
                var peerview = new PeerView({ model: new Peer({ name: computerName, address: window.location.host }) });
                $("#peers_menu ul.dropdown-menu").append(peerview.render().$el);
            }

            this.collection.each(function (node) {
                var nodeview = new NodeView({ model: node });
                var $tr = nodeview.render().$el;
                this.$el.append($tr);
            }, this);

            //render the treeTable
            $("#tree").treeTable({
                initialState: "expanded"
            });

            return this;
        }
    });

    var refreshChart = function () {
        if (graph == null)
            return;
        var curModel = coll.get($('input:radio[name=sensor_radios]:checked').val());
        curModel.set({ "data": null }, { silent: true });
        statusEl.text("Fetching graph data ...");
        curModel.fetch({ data: { start: startDateEl.val(), end: endDateEl.val()} });
    };

    var setTenMinRange = function (e) {
        var now = new Date();
        var past = new Date(now - 10 * 60 * 1000);
        startDateEl.val(rangeConv.format(past));
        endDateEl.val(rangeConv.format(now))
        refreshChart();
    }

    var setTodayRange = function (e) {
        var day = new Date();
        day.setHours(0, 0, 0, 0);
        startDateEl.val(rangeConv.format(day));
        endDateEl.val(rangeConv.format(new Date()))
        refreshChart();
    }
    var setHourRange = function (e) {
        var now = new Date();
        var past = new Date(now - 60 * 60 * 1000);
        startDateEl.val(rangeConv.format(past));
        endDateEl.val(rangeConv.format(now))
        refreshChart();
    }
    var setDayRange = function (e) {
        var now = new Date();
        var past = new Date(now - 24 * 60 * 60 * 1000);
        startDateEl.val(rangeConv.format(past));
        endDateEl.val(rangeConv.format(now))
        refreshChart();
    }
    var setWeekRange = function (e) {
        var now = new Date();
        var past = new Date(now - 7 * 24 * 60 * 60 * 1000);
        startDateEl.val(rangeConv.format(past));
        endDateEl.val(rangeConv.format(now))
        refreshChart();
    }
    var curRangeFn = setTenMinRange;
    var autoFlag = false;

    var setupAutoRefresh = function () {
        if (autoFlag) {
            console.log("Starting auto refresh");
            clearInterval(refreshTimer);
            refreshTimer = setInterval(curRangeFn, 5000);
        }
        else
            clearInterval(refreshTimer);
    }
    var rangeFormat = "%Y-%m-%d %T";
    var rangeConv = new AnyTime.Converter({ format: rangeFormat });


    $("#rangeToday").click(function (e) { curRangeFn = setTodayRange; curRangeFn(); setupAutoRefresh(); });

    $("#rangeTenMinutes").click(function (e) { curRangeFn = setTenMinRange; curRangeFn(); setupAutoRefresh(); });

    $("#rangeHour").click(function (e) { curRangeFn = setHourRange; curRangeFn(); setupAutoRefresh(); });

    $("#rangeDay").click(function (e) { curRangeFn = setDayRange; curRangeFn(); setupAutoRefresh(); });

    $("#rangeWeek").click(function (e) { curRangeFn = setWeekRange; curRangeFn(); setupAutoRefresh(); });

    $("#rangeClear").click(function (e) {
        startDateEl.val("").change();
    });

    var peers = new Peers();
    var peerList = new PeerList({ collection: peers })
    $("#peers_menu").append(peerList.render().el);
    peers.fetch();

    var coll = new Tree();
    var view = new TreeView({ collection: coll })

    $("#sidebar").append(view.render().el);

    coll.fetch();
    setTenMinRange();
    $("#refresh").click(function (e) { curRangeFn(); });
    $("#dismissDates").click(refreshChart);
    $("#rangeTenMinutes").button("toggle");

    $.getJSON('lat.json', function (data) {
        $("#lastAccessTime").text(data.lastAccessTime);
    });

    $("#autoRefresh").click(function () {
        if (!$(this).hasClass('active')) {
            autoFlag = true;
        }
        else
            autoFlag = false;
        setupAutoRefresh();
    });

    startDateEl.focus(function () {
        startDateEl.unbind('focus').AnyTime_picker({ format: rangeFormat, placement: "popup" }); ;
    });
    endDateEl.focus(function () {
        endDateEl.unbind('focus').AnyTime_picker({ format: rangeFormat, placement: "popup" }); ;
    });
});
