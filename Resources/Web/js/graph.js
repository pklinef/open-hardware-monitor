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
                    this.model.get("cid") + this.model.get("sid") + "/sensor.csv",
                    {
                        title: this.model.get("text"),
                        ylabel: this.model.get("type"),
                        labelsDivStyles: { 'textAlign': 'right' }
                    }
                );
            }
            else {
                graph.updateOptions({ 'file': this.model.get("cid") + this.model.get("sid") + '/sensor.csv',
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

    var coll = new Tree();
    var view = new TreeView({ collection: coll })
    $("#sidebar").append(view.render().el);
    coll.fetch();
    var refreshBtn = document.getElementById("refresh");
    refreshBtn.onclick = function () {
        if (graph == null)
            return;
        graph.updateOptions({'file':graph.file_});
    }
});
