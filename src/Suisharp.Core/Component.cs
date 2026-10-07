using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Suisharp.Web")]

namespace Suisharp;

/// <summary>普通のC#オブジェクトとして組み合わせる描画部品。</summary>
public class Component
{
    private readonly List<Component> children = [];
    internal Component? Parent { get; private set; }
    internal string Id { get; } = Guid.NewGuid().ToString("N");
    /// <summary>表示するか。代入だけでは反映せず、Updateが必要です。</summary>
    public bool IsVisible { get; set; } = true;
    private string cssClass = "";
    /// <summary>空白区切りのCSSクラス。変更後にUpdateしてください。</summary>
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public string CssClass
    {
        get => cssClass;
        set => cssClass = string.Join(" ", (value ?? "").Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries).Distinct());
    }
    /// <summary>この部品固有のインラインCSS。変更後にUpdateしてください。</summary>
    public Style Style { get; set; } = new();
    /// <summary>この部品が持つ子部品の配置。変更後にUpdateしてください。</summary>
    public Layout Layout { get; set; } = Layouts.Default;
    internal Action<Component, Action>? RenderRequested { get; set; }

    /// <summary>追加順の子部品。変更にはAdd / Removeを使います。</summary>
    public IReadOnlyList<Component> Children => children.AsReadOnly();

    /// <summary>子を追加します。画面への反映には親のUpdateが必要です。</summary>
    public void Add(params Component[] children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var unique = new HashSet<Component>(ReferenceEqualityComparer.Instance);
        // 全件を先に検証して、失敗時に半分だけ追加されることを防ぐ。
        foreach (var child in children)
        {
            ArgumentNullException.ThrowIfNull(child);
            if (child.Parent is not null || child.RenderRequested is not null || !unique.Add(child))
                throw new InvalidOperationException("同じ部品は複数箇所に追加できません。先にRemoveしてください。");
            for (Component? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
                if (ReferenceEquals(ancestor, child))
                    throw new InvalidOperationException("部品ツリーを循環させることはできません。");
        }
        foreach (var child in children)
        {
            child.Parent = this;
            this.children.Add(child);
        }
    }

    /// <summary>直接の子を外します。画面への反映にはこの親のUpdateが必要です。</summary>
    public void Remove(Component child)
    {
        ArgumentNullException.ThrowIfNull(child);
        var index = children.FindIndex(candidate => ReferenceEquals(candidate, child));
        if (index < 0) return;
        children.RemoveAt(index);
        child.Parent = null;
    }

    /// <summary>
    /// この部品自身を描画更新します。既定では各子のvirtual Updateを呼びます。
    /// プロパティ変更やAdd / Removeだけでは描画されません。
    /// 未表示でもC#の更新処理は実行しますが送信しません。構造変更後は親をUpdateしてください。
    /// 必要な子だけUpdateするよう通常のC# overrideで変更できます。
    /// ブラウザで描画が完了するまで待機するメソッドではありません。
    /// </summary>
    public virtual void Update()
    {
        var root = this;
        while (root.Parent is not null) root = root.Parent;
        void UpdateChildren()
        {
            foreach (var child in children.ToArray())
                if (ReferenceEquals(child.Parent, this)) child.Update();
        }
        if (root.RenderRequested is { } render) render(this, UpdateChildren);
        else UpdateChildren();
    }
}
