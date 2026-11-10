CC      ?= cc
# -march=native: the C version is tuned for the machine it runs on.
# No -ffast-math: it would change NaN/inf semantics relative to C# and Rust.
CFLAGS  ?= -O3 -march=native -flto -std=gnu11 -Wall -Wextra -Wshadow
LDFLAGS ?= -flto

all: bench test tui

bench: bench.c de.h problems.h
	$(CC) $(CFLAGS) -o $@ bench.c $(LDFLAGS)

test: test.c de.h problems.h
	$(CC) $(CFLAGS) -o $@ test.c $(LDFLAGS) -lm

tui: tui.c de.h problems.h
	$(CC) $(CFLAGS) -o $@ tui.c $(LDFLAGS) -lm

check: test
	./test

clean:
	rm -f bench test tui

.PHONY: all check clean
