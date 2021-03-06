/*
Copyright 2007-2010 WebDriver committers
Copyright 2007-2010 Google Inc.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

     http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

package org.openqa.selenium.interactions;

import org.openqa.selenium.Mouse;
import org.openqa.selenium.interactions.internal.MouseAction;
import org.openqa.selenium.internal.Locatable;

/**
 * Releases the left mouse button
 *
 */

public class ButtonReleaseAction extends MouseAction implements Action {
  public ButtonReleaseAction(Mouse mouse, Locatable locationProvider) {
    super(mouse, locationProvider);
  }

  /**
   * Releases the mouse button currently left held. This action can be called
   * for an element different than the one ClickAndHoldAction was called for.
   * However, if this action is performed out of sequence (without holding
   * down the mouse button, for example) the results will be different
   * between browsers.
   */
  public void perform() {
    moveToLocation();
    mouse.mouseUp(getActionLocation());
  }
}
