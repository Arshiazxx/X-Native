using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using XNative.Core.Abstractions;
using XNative.Core.Games;

namespace XNative.Wine.Profiles
{
    /// <summary>
    /// An <see cref="IGameProfileStore"/> loaded from a JSON compatibility
    /// database. The document is a JSON array of objects with the shape
    /// <c>{ "id": "...", "executableName": "game.exe", "rating": "Playable",
    /// "notes": "...", "recommendedArguments": "..." }</c>, where
    /// <c>rating</c> is a <see cref="CompatibilityRating"/> member name
    /// (matched case-insensitively) and <c>notes</c> /
    /// <c>recommendedArguments</c> are optional.
    /// </summary>
    /// <remarks>
    /// A malformed database is loud, never silently empty: invalid JSON,
    /// a non-array root, a missing <c>id</c> / <c>executableName</c> /
    /// <c>rating</c>, an empty or whitespace <c>id</c>, a duplicated
    /// <c>id</c>, or an unknown rating name all throw
    /// <see cref="InvalidDataException"/> at load time. Silently treating
    /// a broken compatibility database as "no profiles" would hide data
    /// loss and make known-broken games look merely untested, and a
    /// duplicated ID would silently shadow the earlier profile. IDs are
    /// compared case-insensitively, matching how the launch pipeline
    /// resolves a requested profile ID.
    /// </remarks>
    public sealed class JsonGameProfileStore : IGameProfileStore
    {
        private readonly InMemoryGameProfileStore _inner;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="JsonGameProfileStore"/> class from a JSON string.
        /// </summary>
        /// <param name="json">The JSON compatibility database.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the JSON is malformed or a profile entry is invalid.</exception>
        public JsonGameProfileStore(string json)
            : this(Parse(json))
        {
        }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="JsonGameProfileStore"/> class from a reader. The
        /// reader is read to the end but not disposed; its lifetime stays
        /// with the caller.
        /// </summary>
        /// <param name="reader">The reader supplying the JSON compatibility database.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="reader"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidDataException">Thrown when the JSON is malformed or a profile entry is invalid.</exception>
        public JsonGameProfileStore(TextReader reader)
            : this(ReadAll(reader))
        {
        }

        private JsonGameProfileStore(List<GameProfile> profiles)
        {
            _inner = new InMemoryGameProfileStore(profiles);
        }

        /// <summary>
        /// Creates a store from a JSON string. Equivalent to the
        /// <see cref="JsonGameProfileStore(string)"/> constructor, provided
        /// for call sites that read more naturally as a factory.
        /// </summary>
        /// <param name="json">The JSON compatibility database.</param>
        /// <returns>The loaded store.</returns>
        public static JsonGameProfileStore FromJson(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            return new JsonGameProfileStore(json);
        }

        /// <summary>
        /// Creates a store from a reader. Equivalent to the
        /// <see cref="JsonGameProfileStore(TextReader)"/> constructor.
        /// </summary>
        /// <param name="reader">The reader supplying the JSON compatibility database.</param>
        /// <returns>The loaded store.</returns>
        public static JsonGameProfileStore FromReader(TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return new JsonGameProfileStore(reader);
        }

        /// <inheritdoc />
        public IReadOnlyCollection<GameProfile> GetProfiles()
        {
            return _inner.GetProfiles();
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="game"/> is <c>null</c>.</exception>
        public GameProfile? FindForGame(GameInfo game)
        {
            return _inner.FindForGame(game);
        }

        private static string ReadAll(TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            return reader.ReadToEnd();
        }

        private static List<GameProfile> Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The compatibility profile database is not valid JSON: " + exception.Message,
                    exception);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidDataException(
                        "The compatibility profile database must be a JSON array of profile objects.");
                }

                var profiles = new List<GameProfile>();
                var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int index = 0;
                foreach (JsonElement element in document.RootElement.EnumerateArray())
                {
                    GameProfile profile = ParseProfile(element, index);
                    if (!seenIds.Add(profile.Id))
                    {
                        throw new InvalidDataException(
                            "Compatibility profile at index " + index + " duplicates the profile ID '" + profile.Id +
                            "'. Profile IDs must be unique (compared case-insensitively); " +
                            "a duplicate would silently shadow the earlier profile.");
                    }

                    profiles.Add(profile);
                    index++;
                }

                return profiles;
            }
        }

        private static GameProfile ParseProfile(JsonElement element, int index)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException(
                    "Compatibility profile at index " + index + " must be a JSON object.");
            }

            string id = GetRequiredString(element, "id", index);
            string executableName = GetRequiredString(element, "executableName", index);
            string ratingName = GetRequiredString(element, "rating", index);
            string? notes = GetOptionalString(element, "notes", index);
            string? recommendedArguments = GetOptionalString(element, "recommendedArguments", index);

            CompatibilityRating rating;
            if (!Enum.TryParse(ratingName, true, out rating) || !Enum.IsDefined(typeof(CompatibilityRating), rating))
            {
                throw new InvalidDataException(
                    "Compatibility profile '" + id + "' has an unknown rating '" + ratingName + "'.");
            }

            try
            {
                return new GameProfile(id, executableName, rating, notes, recommendedArguments);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    "Compatibility profile at index " + index + " is invalid: " + exception.Message,
                    exception);
            }
        }

        private static string GetRequiredString(JsonElement element, string propertyName, int index)
        {
            JsonElement value;
            if (!element.TryGetProperty(propertyName, out value) || value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    "Compatibility profile at index " + index + " is missing a string '" + propertyName + "' property.");
            }

            return value.GetString()!;
        }

        private static string? GetOptionalString(JsonElement element, string propertyName, int index)
        {
            JsonElement value;
            if (!element.TryGetProperty(propertyName, out value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidDataException(
                    "Compatibility profile at index " + index + " has a non-string '" + propertyName + "' property.");
            }

            return value.GetString();
        }
    }
}
