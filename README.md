# PathFinder
## Requirements

- .NET 10 SDK or newer

No external dependencies — `dotnet build` does restore and build in one step.

## Build

```sh
dotnet build PathFinder.slnx -c Release
```

The executable lands in `PathFinder/bin/Release/net10.0/`

## Run

Pass the input file as an argument:

```sh
dotnet run --project PathFinder/PathFinder.csproj -c Release -- path/to/Data.txt
```

Or run the built binary:

```sh
./PathFinder/bin/Release/net10.0/PathFinder path/to/Data.txt
```

With no argument, the app prompts for the file path.

Pick an item by its number to print the directions to it; press Enter on an
empty line to exit.

## Input format

The input is a UTF-8 text file holding a `tree`-style hierarchy: directions are
interior nodes, items are leaves. The parser is strict — any deviation aborts
the load and reports the offending line number.

### Line shape

Every line is `<indent><connector><marker><text>`:

| Line | Shape | Example |
| --- | --- | --- |
| Root direction | no indent, no connector, marker `+ ` | `+ Warehouse` |
| Direction | indent, connector, then `+ ` | `├──+ Aisle 1` |
| Item | indent, connector, then ` Item: ` | `├── Item: Hammer` |

Note the asymmetry: the direction marker `+ ` follows the connector with **no
space between them**, while the item marker ` Item: ` carries **one leading
space**. `├── + Aisle 1` and `├──Item: Hammer` are both rejected.

### Example

```text
+ Warehouse
├──+ Aisle 1
|  └── Item: Hammer
└──+ Aisle 2
   └── Item: Wrench
```

### Indentation and connectors

- Each indent column is exactly three characters: `|  ` (pipe plus two spaces)
  or three spaces. Tabs are not accepted.
- A line at depth *d* carries *d*−1 indent columns, then its connector.
- The connector is `├──` when more siblings follow at that depth, or `└──` when
  the line is the last child.

### Structural rules

- Depth may increase by at most one per line; a deeper jump reports
  `Depth is wrong.`
- Once a depth has been closed by `└──`, no further sibling may appear at that
  depth: `a last child connector already closed this level.`
- Direction text may not be empty or whitespace.
- Item names are trimmed and may not be empty. They must be unique across the
  whole file, compared case-insensitively — `Milk` and `milk` collide even in
  different branches, and the second occurrence reports
  `Duplicate item name '...'`.
- Maximum nesting is 31 levels below the root.
- Several root lines are accepted and parsed as a forest; each item resolves
  against the root above it.

### Encoding and line endings

- UTF-8 only. A UTF-8 BOM is skipped if present. UTF-16 and UTF-32 BOMs are
  rejected with `File is not in expected format.`
- Lines may end `LF` or `CRLF`. The final line need not be terminated.
- An empty file is valid and yields no items.

### Current limitations

- Blank lines are rejected anywhere in the file, including a single trailing
  blank line after the last entry.
- A single line may not exceed 4096 bytes.

## Decisions

### Optimize for large input files

**Context.** Only small and medium sample files were provided, with no stated
upper bound on size. Reading a file into memory as text is also not free:
decoding UTF-8 into .NET `string` instances (UTF-16) roughly doubles the
footprint for Latin-script text, so `File.ReadAllLines` approach costs
well over the file's size on disk.

**Decision.** During the parse, each direction node stores only its
`(offset, length)` in the file; no direction text is decoded or retained. Text
is read back on demand.

**Consequences.** Resident memory scales with the number of *nodes*, not with
the size of the text — a node is 12 bytes of position plus a parent reference.
The parse decodes to `string` in exactly one place: item names, which must be
held to be listed and deduplicated.

### Keep the file handle open for the document's lifetime

**Context.** Storing offsets instead of text means the file becomes part of the
app's state. If the file changes after the parse, the stored offsets point at
the wrong bytes, and the app would confidently print wrong or corrupt
directions — a worse failure than an error.

**Options considered.**

| Option | Verdict |
| --- | --- |
| Hash the file at load and re-verify before each read | Rejected — re-hashing means a full re-read of the file on every lookup, which defeats the point of not reading it all in the first place. |
| Record last-modified time at load and compare before each read | Viable and cheap, but detects the change only *after* it has happened, and the timestamp is coarse and forgeable. |
| Hold the `FileStream` open for the document's lifetime | **Chosen.** |

**Decision.** `DirectionDocument` owns the `FileStream` from `LoadAsync` until
`Dispose`, opened with `FileShare.Read`.

**Consequences.** `FileShare.Read` admits other readers but refuses writers, so
the file cannot be modified out from under the stored offsets while the
document is open.

### Multiple roots

**Context.** I didnt know if multiple roots are allowed.

**Decision.** Allow it.


## Next steps

### Paging in the console (primary gap)

The document layer is built for input of unbounded size, but the console layer
still assumes the item list fits on one screen. `Program` prints every item in
a single pass before entering the selection loop.
