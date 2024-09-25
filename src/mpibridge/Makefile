CFLAGS= -Wall -Werror -pedantic -O3 -fPIC `pkg-config --cflags --libs ompi`
LIB=mpibridge.so

.PHONY: all clean
all: $(LIB)
clean:
	$(RM) $(LIB)

$(LIB): mpibridge.c
	$(CC) -I $(CFLAGS) -shared $^ -o $@
