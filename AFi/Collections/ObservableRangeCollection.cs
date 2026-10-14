using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace AFi.Collections;

/// <summary>
/// Расширение ObservableCollection с операцией замены содержимого
/// одним событием. Стандартный Clear() + Add() в цикле генерирует
/// N событий CollectionChanged, каждое из которых дёргает перерисовку
/// CollectionView. ReplaceRange отправляет одно событие Reset —
/// это в разы быстрее при больших объёмах.
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

        // Одно событие Reset — вместо N событий Add
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));
    }
}