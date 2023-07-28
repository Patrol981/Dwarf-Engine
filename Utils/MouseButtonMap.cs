﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dwarf.Extensions.GLFW;
public static class MouseButtonMap {
  public enum Buttons {
    GLFW_MOUSE_BUTTON_1 = 0,
    GLFW_MOUSE_BUTTON_2 = 1,
    GLFW_MOUSE_BUTTON_3 = 2,
    GLFW_MOUSE_BUTTON_4 = 3,
    GLFW_MOUSE_BUTTON_5 = 4,
    GLFW_MOUSE_BUTTON_6 = 5,
    GLFW_MOUSE_BUTTON_7 = 6,
    GLFW_MOUSE_BUTTON_8 = 7,
    GLFW_MOUSE_BUTTON_LAST = GLFW_MOUSE_BUTTON_8,
    GLFW_MOUSE_BUTTON_LEFT = GLFW_MOUSE_BUTTON_1,
    GLFW_MOUSE_BUTTON_RIGHT = GLFW_MOUSE_BUTTON_2,
    GLFW_MOUSE_BUTTON_MIDDLE = GLFW_MOUSE_BUTTON_3
  }

  public enum Action {
    GLFW_PRESS = 1,
    GLFW_RELEASE = 0
  }
}
