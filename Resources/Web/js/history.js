$(window).load(function(){
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
        "click input[type=checkbox]": "plot"
    },
    render: function (eventName) {

        var html = this.template(this.model.toJSON());
        this.setElement($(html));
        return this;
    },
    plot: function () {
        console.log(this.model.get("sid"));
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
$("body").append(view.render().el);
coll.fetch();
});
