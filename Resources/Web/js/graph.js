$(window).load(function () {
    var chart;
    var graph;
    var curSeries;

    //keeping track of the two datefields
    //so that we don't traverse the DOM tree every time
    var startDateEl = $("#rangeStart");
    var endDateEl = $("#rangeEnd");

    var Node = Backbone.Model.extend({
        defaults: {
            id: '',
            text: '',
            parent: '',
            type: '',
            imageURL: '',
            data:''
        },
        initialize: function () {
            this.bind("change:data", this.parseData);
        },
        parseData: function (e) {
            //convert the first column to Date objects
            var newData = _.map(this.get("data"), function(row){ return [new Date(row[0]), row[1]]; });
            this.set({"data":null}, {silent: true});
            this.set({"data":newData}, {silent: true});
            this.updateChart();
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
        url: '/sensors',
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
            //if (chart) chart.showLoading();
            this.model.set({"data":null}, {silent: true});
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
        curModel.set({"data":null}, {silent: true});
        //chart.showLoading();
        curModel.fetch({ data: { start: startDateEl.val(), end: endDateEl.val()} });
    };

    var setDefaultRange = function(e) {
          var now = new Date();
          var past = new Date(now - 10*60*1000);
          startDateEl.val(rangeConv.format(past));
          endDateEl.val(rangeConv.format(now)) 
          refreshChart();
    }

    var rangeFormat = "%Y-%m-%d %T";
    var rangeConv = new AnyTime.Converter({format:rangeFormat});

    $("#rangeToday").click( function(e) {
      var day = new Date();
      day.setHours(0,0,0,0);
      startDateEl.val(rangeConv.format(day));
      endDateEl.val(rangeConv.format(new Date())) 
      refreshChart();
    });

    $("#rangeTenMinutes").click(setDefaultRange);

    $("#rangeHour").click( function(e) {
      var now = new Date();
      var past = new Date(now - 60*60*1000);
      startDateEl.val(rangeConv.format(past));
      endDateEl.val(rangeConv.format(now)) 
      refreshChart();
    });

    $("#rangeDay").click( function(e) {
      var now = new Date();
      var past = new Date(now - 24*60*60*1000);
      startDateEl.val(rangeConv.format(past));
      endDateEl.val(rangeConv.format(now)) 
      refreshChart();
    });

    $("#rangeWeek").click( function(e) {
      var now = new Date();
      var past = new Date(now - 7*24*60*60*1000);
      startDateEl.val(rangeConv.format(past));
      endDateEl.val(rangeConv.format(now)) 
      refreshChart();
    });

    $("#rangeClear").click( function(e) {
      startDateEl.val("").change(); } );

    startDateEl.AnyTime_picker({format:rangeFormat});
    endDateEl.AnyTime_picker({format:rangeFormat});

    var coll = new Tree();
    var view = new TreeView({ collection: coll })

    $("#sidebar").append(view.render().el);

    coll.fetch();
    setDefaultRange();
    $("#refresh").click(refreshChart);

    $.getJSON('lat.json', function(data) {
        $("#lastAccessTime").text(data.lastAccessTime);
    });
});
