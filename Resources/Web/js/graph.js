$(window).load(function () {
    var graph;
    var Node = Backbone.Model.extend({
        defaults: {
            id: '',
            text: '',
            parent: '',
            sid: '',
            cid: '',
            type: '',
            imageURL: ''
        },
        initialize: function () {
        }
    });

    var Tree = Backbone.Collection.extend({
        model: Node,
        url: '/tree.json',
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
            if (graph == null) {
                graph = new Dygraph(
                    document.getElementById("sensor"),
                    this.model.get("cid") + this.model.get("sid") + "/sensor.csv?start=" + $("#rangeStart").val() + '&end=' + $("#rangeEnd").val(),
                    {
                        title: this.model.get("text"),
                        ylabel: this.model.get("type"),
                        labelsDivStyles: { 'textAlign': 'right' }
                    }
                );
            }
            else {
                graph.updateOptions({ 'file': this.model.get("cid") + this.model.get("sid") + '/sensor.csv?start=' + $("#rangeStart").val() + '&end=' + $("#rangeEnd").val(),
                    'title': this.model.get("text"),
                    'ylabel': this.model.get("type")
                });
            }
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

    var setDefaultRange = function(e) {
          var now = new Date();
          var past = new Date(now - 10*60*1000);
          $("#rangeStart").val(rangeConv.format(past));
          $("#rangeEnd").val(rangeConv.format(now)) 
    }

    var rangeFormat = "%Y-%m-%d %T";
    var rangeConv = new AnyTime.Converter({format:rangeFormat});

    $("#rangeToday").click( function(e) {
      var day = new Date();
      day.setHours(0,0,0,0);
      $("#rangeStart").val(rangeConv.format(day));
      $("#rangeEnd").val(rangeConv.format(new Date())) } );

    $("#rangeTenMinutes").click(setDefaultRange);

    $("#rangeHour").click( function(e) {
      var now = new Date();
      var past = new Date(now - 60*60*1000);
      $("#rangeStart").val(rangeConv.format(past));
      $("#rangeEnd").val(rangeConv.format(now)) } );

    $("#rangeDay").click( function(e) {
      var now = new Date();
      var past = new Date(now - 24*60*60*1000);
      $("#rangeStart").val(rangeConv.format(past));
      $("#rangeEnd").val(rangeConv.format(now)) } );

    $("#rangeWeek").click( function(e) {
      var now = new Date();
      var past = new Date(now - 7*24*60*60*1000);
      $("#rangeStart").val(rangeConv.format(past));
      $("#rangeEnd").val(rangeConv.format(now)) } );

    $("#rangeClear").click( function(e) {
      $("#rangeStart").val("").change(); } );

    $("#rangeStart").AnyTime_picker({format:rangeFormat});
    $("#rangeEnd").AnyTime_picker({format:rangeFormat});

    var refreshBtn = document.getElementById("refresh");
    refreshBtn.onclick = function () {
        if (graph == null)
            return;
        graph.updateOptions({'file':graph.file_.replace(/start=.*/, 'start=' + $("#rangeStart").val() + '&end=' + $("#rangeEnd").val())});
    }

    var coll = new Tree();
    var view = new TreeView({ collection: coll })
    $("#sidebar").append(view.render().el);
    coll.fetch();
    setDefaultRange();
});
