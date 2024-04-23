namespace Dwarf.Globals;
public sealed class JoystickState {
  private static JoystickState s_instance = null!;
  public static unsafe void JoystickCallback(int jid, int j_event) {
    Console.WriteLine(jid + " " + j_event);
  }

  public JoystickState GetInstance() {
    if (s_instance == null) {
      s_instance = new JoystickState();
    }
    return s_instance;
  }
}
