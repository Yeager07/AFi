using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AFi.Collections;

/// <summary>
/// Расширение ObservableCollection с операциями пакетного изменения.
/// Стандартный Clear() + Add() в цикле генерирует N событий CollectionChanged,
/// каждое из которых дёргает перерисовку CollectionView. Одно событие Reset
/// в разы быстрее при больших объёмах.
/// </summary>
public class ObservableRangeCollection<T> : ObservableCollection<T>
{
    /// <summary>
    /// Полностью заменяет содержимое коллекции, отправляя
    /// единственное событие Reset. UI перерисовывается один раз.
    /// </summary>
    public void ReplaceRange(IEnumerable<T> items)
    {
        if (items is null) return;

        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }

    /// <summary>
    /// Дополняет коллекцию новыми элементами одним событием Reset.
    /// Существующие элементы не удаляются.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items is null) return;

        var added = false;
        foreach (var item in items)
        {
            Items.Add(item);
            added = true;
        }

        if (!added) return;

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
}