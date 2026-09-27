using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace KeyButtonMaker;

/// <summary>
/// 設定内のすべての項目の変更を監視し、少し待ってから自動保存する。
/// 連続した編集(文字入力など)はまとめて 1 回の保存にする。
/// </summary>
public sealed class AutoSaver
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer;

    public AutoSaver(AppSettings settings)
    {
        _settings = settings;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += (_, _) => Flush();
        Watch(settings.Profiles);
    }

    public void RequestSave()
    {
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>保留中の変更があれば直ちに保存する</summary>
    public void Flush()
    {
        _timer.Stop();
        try { SettingsStore.Save(_settings); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "設定の保存に失敗しました", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Watch(object? item)
    {
        switch (item)
        {
            case Profile p: Subscribe(p); Watch(p.Shortcuts); break;
            case Shortcut s: Subscribe(s); Watch(s.Trigger); Watch(s.Steps); break;
            case MacroStep m: Subscribe(m); Watch(m.Key); break;
            case KeyCombo k: Subscribe(k); break;
        }
        if (item is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnCollectionChanged;
            foreach (var child in (IEnumerable)collection) Watch(child);
        }
    }

    private void Subscribe(INotifyPropertyChanged item) => item.PropertyChanged += (_, _) => RequestSave();

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // 追加された項目も監視対象にする(削除された項目は変更されないので放置でよい)
        // (Move でも NewItems が入るので、二重登録しないよう Add/Replace に限る)
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace && e.NewItems is not null)
            foreach (var item in e.NewItems) Watch(item);
        RequestSave();
    }
}
