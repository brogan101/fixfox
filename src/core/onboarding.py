from __future__ import annotations

from dataclasses import dataclass


@dataclass
class OnboardingState:
    completed: bool = False
    skipped: bool = False
    dont_show_again: bool = False
    selected_goal: str = "speed"

    def should_show(self) -> bool:
        return not self.completed and not self.dont_show_again

    def mark_skipped(self, dont_show_again: bool) -> None:
        self.skipped = True
        self.dont_show_again = dont_show_again
        if dont_show_again:
            self.completed = True

    def complete(self, goal: str, dont_show_again: bool = True) -> None:
        self.selected_goal = goal
        self.completed = True
        self.dont_show_again = dont_show_again
