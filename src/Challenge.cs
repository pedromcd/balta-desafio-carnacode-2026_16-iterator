using System;
using System.Collections.Generic;
using System.Linq;

namespace DesignPatternChallenge
{
    // ============================
    // 1) Modelo
    // ============================
    public class Song
    {
        public string Title { get; }
        public string Artist { get; }
        public string Genre { get; }
        public int DurationSeconds { get; }
        public int Year { get; }

        public Song(string title, string artist, string genre, int duration, int year)
        {
            Title = title;
            Artist = artist;
            Genre = genre;
            DurationSeconds = duration;
            Year = year;
        }

        public override string ToString() => $"{Title} - {Artist} ({Genre}, {Year})";
    }

    // ============================
    // 2) Iterator Pattern (Interface)
    // ============================
    public interface IIterator<T>
    {
        bool HasNext();
        T Next();
        void Reset();
    }

    // ============================
    // 3) Aggregate (Interface): qualquer coleção que "cria iteradores"
    // ============================
    public interface IAggregate<T>
    {
        IIterator<T> CreateIterator();
    }

    // ============================
    // 4) Playlist (Aggregate) - NÃO expõe List
    // ============================
    public class Playlist : IAggregate<Song>
    {
        public string Name { get; }
        private readonly List<Song> _songs = new List<Song>();

        public Playlist(string name) => Name = name;

        public void AddSong(Song song) => _songs.Add(song);

        // snapshot interno para o iterador não depender da lista mutável
        internal IReadOnlyList<Song> Snapshot() => _songs.ToList();

        // Iterador padrão (sequencial)
        public IIterator<Song> CreateIterator() => new SequentialIterator<Song>(Snapshot());

        // Iteradores personalizados
        public IIterator<Song> CreateShuffleIterator(int? seed = null) =>
            new ShuffleIterator<Song>(Snapshot(), seed);

        public IIterator<Song> CreateGenreIterator(string genre) =>
            new FilterIterator<Song>(
                Snapshot(),
                s => string.Equals(s.Genre, genre, StringComparison.OrdinalIgnoreCase)
            );

        public IIterator<Song> CreateOldiesIterator(int yearCutoff = 2000) =>
            new FilterIterator<Song>(Snapshot(), s => s.Year < yearCutoff, orderBy: s => s.Year);
    }

    // ============================
    // 5) Biblioteca (Aggregate) - estrutura interna complexa NÃO é exposta
    // ============================
    public class MusicLibrary : IAggregate<Song>
    {
        private readonly Dictionary<string, List<Song>> _songsByGenre = new Dictionary<string, List<Song>>();
        private readonly Dictionary<string, List<Song>> _songsByArtist = new Dictionary<string, List<Song>>();

        public void AddSong(Song song)
        {
            if (!_songsByGenre.ContainsKey(song.Genre))
                _songsByGenre[song.Genre] = new List<Song>();
            _songsByGenre[song.Genre].Add(song);

            if (!_songsByArtist.ContainsKey(song.Artist))
                _songsByArtist[song.Artist] = new List<Song>();
            _songsByArtist[song.Artist].Add(song);
        }

        // Iterador que percorre toda a biblioteca sem expor dicionários
        public IIterator<Song> CreateIterator()
        {
            // Achatando (flatten) a estrutura interna
            var allSongs = _songsByGenre.Values.SelectMany(list => list).ToList();
            return new SequentialIterator<Song>(allSongs);
        }

        public IIterator<Song> CreateGenreIterator(string genre)
        {
            if (!_songsByGenre.TryGetValue(genre, out var songs))
                songs = new List<Song>();

            return new SequentialIterator<Song>(songs.ToList());
        }

        public IIterator<Song> CreateArtistIterator(string artist)
        {
            if (!_songsByArtist.TryGetValue(artist, out var songs))
                songs = new List<Song>();

            return new SequentialIterator<Song>(songs.ToList());
        }
    }

    // ============================
    // 6) Iteradores Concretos (Genéricos)
    // ============================
    public class SequentialIterator<T> : IIterator<T>
    {
        private readonly IReadOnlyList<T> _items;
        private int _index;

        public SequentialIterator(IReadOnlyList<T> items)
        {
            _items = items;
            _index = 0;
        }

        public bool HasNext() => _index < _items.Count;

        public T Next()
        {
            if (!HasNext()) throw new InvalidOperationException("Sem próximos itens.");
            return _items[_index++];
        }

        public void Reset() => _index = 0;
    }

    public class ShuffleIterator<T> : IIterator<T>
    {
        private readonly List<T> _shuffled;
        private int _index;

        public ShuffleIterator(IReadOnlyList<T> items, int? seed = null)
        {
            var rand = seed.HasValue ? new Random(seed.Value) : new Random();
            _shuffled = items.OrderBy(_ => rand.Next()).ToList();
            _index = 0;
        }

        public bool HasNext() => _index < _shuffled.Count;

        public T Next()
        {
            if (!HasNext()) throw new InvalidOperationException("Sem próximos itens.");
            return _shuffled[_index++];
        }

        public void Reset() => _index = 0;
    }

    // Iterador com filtro (e ordenação opcional)
    public class FilterIterator<T> : IIterator<T>
    {
        private readonly List<T> _filtered;
        private int _index;

        public FilterIterator(
            IReadOnlyList<T> items,
            Func<T, bool> predicate,
            Func<T, object> orderBy = null)
        {
            IEnumerable<T> query = items.Where(predicate);
            if (orderBy != null) query = query.OrderBy(orderBy);

            _filtered = query.ToList();
            _index = 0;
        }

        public bool HasNext() => _index < _filtered.Count;

        public T Next()
        {
            if (!HasNext()) throw new InvalidOperationException("Sem próximos itens.");
            return _filtered[_index++];
        }

        public void Reset() => _index = 0;
    }

    // ============================
    // 7) Adapters de coleção (Array e Queue) para a MESMA interface
    // ============================
    public class ArrayAggregate<T> : IAggregate<T>
    {
        private readonly T[] _array;
        public ArrayAggregate(T[] array) => _array = array ?? Array.Empty<T>();
        public IIterator<T> CreateIterator() => new SequentialIterator<T>(_array.ToList());
    }

    public class QueueAggregate<T> : IAggregate<T>
    {
        private readonly Queue<T> _queue;
        public QueueAggregate(Queue<T> queue) => _queue = queue ?? new Queue<T>();

        public IIterator<T> CreateIterator()
        {
            // Importante: não consumir (Dequeue) — só snapshot
            return new SequentialIterator<T>(_queue.ToList());
        }
    }

    // ============================
    // 8) MusicPlayer: só toca qualquer coisa via Iterator
    // ============================
    public class MusicPlayer
    {
        public void Play(string title, IIterator<Song> iterator)
        {
            Console.WriteLine($"\n=== {title} ===");

            int i = 1;
            while (iterator.HasNext())
            {
                Console.WriteLine($"{i++}. {iterator.Next()}");
            }
        }
    }

    // ============================
    // 9) Demo
    // ============================
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Sistema de Playlist (Iterator Pattern) ===");

            var playlist = new Playlist("Minhas Favoritas");
            playlist.AddSong(new Song("Bohemian Rhapsody", "Queen", "Rock", 354, 1975));
            playlist.AddSong(new Song("Imagine", "John Lennon", "Pop", 183, 1971));
            playlist.AddSong(new Song("Smells Like Teen Spirit", "Nirvana", "Rock", 301, 1991));
            playlist.AddSong(new Song("Billie Jean", "Michael Jackson", "Pop", 294, 1982));
            playlist.AddSong(new Song("Hotel California", "Eagles", "Rock", 391, 1976));
            playlist.AddSong(new Song("Sweet Child O' Mine", "Guns N' Roses", "Rock", 356, 1987));

            var player = new MusicPlayer();

            // Playlist: diferentes navegações SEM o player conhecer List
            player.Play($"Tocando {playlist.Name} (Sequencial)", playlist.CreateIterator());
            player.Play($"Tocando {playlist.Name} (Aleatório)", playlist.CreateShuffleIterator());
            player.Play($"Tocando {playlist.Name} (Gênero: Rock)", playlist.CreateGenreIterator("Rock"));
            player.Play($"Tocando {playlist.Name} (Antigas < 2000)", playlist.CreateOldiesIterator());

            // Múltiplas iterações simultâneas (independentes)
            Console.WriteLine("\n=== Múltiplas iterações simultâneas ===");
            var it1 = playlist.CreateGenreIterator("Pop");
            var it2 = playlist.CreateOldiesIterator();

            Console.WriteLine("Iterador 1 (Pop) pega 1:");
            if (it1.HasNext()) Console.WriteLine(it1.Next());

            Console.WriteLine("Iterador 2 (Oldies) pega 1:");
            if (it2.HasNext()) Console.WriteLine(it2.Next());

            Console.WriteLine("Iterador 1 (Pop) continua:");
            while (it1.HasNext()) Console.WriteLine(it1.Next());

            // Array e Queue tocando do MESMO jeito
            Console.WriteLine("\n=== Tocando de Array e Queue (mesma interface) ===");
            Song[] arr = {
                new Song("Numb", "Linkin Park", "Rock", 185, 2003),
                new Song("Hey Jude", "The Beatles", "Rock", 431, 1968)
            };

            var q = new Queue<Song>();
            q.Enqueue(new Song("Shape of You", "Ed Sheeran", "Pop", 233, 2017));
            q.Enqueue(new Song("Thriller", "Michael Jackson", "Pop", 357, 1982));

            player.Play("Array (Sequencial)", new ArrayAggregate<Song>(arr).CreateIterator());
            player.Play("Queue (Sequencial)", new QueueAggregate<Song>(q).CreateIterator());

            // Biblioteca com estrutura interna complexa (sem expor dicionários)
            Console.WriteLine("\n=== Biblioteca (estrutura interna escondida) ===");
            var library = new MusicLibrary();
            library.AddSong(new Song("One", "Metallica", "Rock", 447, 1988));
            library.AddSong(new Song("Smooth", "Santana", "Pop", 295, 1999));
            library.AddSong(new Song("Back In Black", "AC/DC", "Rock", 255, 1980));

            player.Play("Biblioteca (todas)", library.CreateIterator());
            player.Play("Biblioteca (Rock)", library.CreateGenreIterator("Rock"));

            Console.WriteLine("\n=== O que foi resolvido ===");
            Console.WriteLine("✓ Playlist não expõe List internamente");
            Console.WriteLine("✓ Player não repete lógica de iteração");
            Console.WriteLine("✓ Iteração uniforme para Playlist/Array/Queue/Library");
            Console.WriteLine("✓ Iteradores customizados (shuffle, filtro, oldies)");
            Console.WriteLine("✓ Múltiplas travessias simultâneas e independentes");
        }
    }
}