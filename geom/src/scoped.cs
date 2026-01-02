using static br.Br;

namespace br {

// honestly these so useful why they not in the standard. lowk maybe it is im
// bad at looking.

public static class Scoped {
    public class OnLeave : IDisposable {
        public Action action { get; }
        public OnLeave(Action action) { this.action = action; }
        public void Dispose() { action(); }
    }
    public static IDisposable on_leave(Action action) => new OnLeave(action);

    public sealed class DoNothing : IDisposable {
        public DoNothing() {}
        public void Dispose() {}
    }
    public static IDisposable do_nothing() => new DoNothing();
}

}
