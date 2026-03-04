from __future__ import annotations

from .theme import BASE_FONT_FAMILY, ThemeTokens, normalize_density, resolve_density_tokens


def _hex_to_rgb(color: str) -> tuple[int, int, int]:
    value = color.strip().lstrip("#")
    if len(value) != 6:
        return (0, 0, 0)
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))


def _alpha(color: str, a: float) -> str:
    r, g, b = _hex_to_rgb(color)
    return f"rgba({r}, {g}, {b}, {a:.3f})"


def build_qss(tokens: ThemeTokens, mode: str, density: str) -> str:
    del mode
    density_key = normalize_density(density)
    d = resolve_density_tokens(density_key)
    scroll_size = 10 if density_key == "compact" else 12
    splitter_size = 6 if density_key == "compact" else 8

    focus_ring = _alpha(tokens.accent, 0.48)
    accent_tint = _alpha(tokens.accent, 0.15)
    accent_tint_strong = _alpha(tokens.accent, 0.24)
    border_soft = _alpha(tokens.border, 0.78)
    border_subtle = _alpha(tokens.border, 0.56)
    surface_raised = _alpha(tokens.panel2, 0.96)
    surface_alt = _alpha(tokens.shadow2, 0.24)
    text_soft = _alpha(tokens.text, 0.76)
    disabled_bg = _alpha(tokens.panel2, 0.48)
    disabled_text = _alpha(tokens.text_muted, 0.74)

    return f"""
QWidget {{
  color: {tokens.text};
  background: transparent;
  font-family: {BASE_FONT_FAMILY};
  font-size: {d.font_size}pt;
}}

QMainWindow,
QMainWindow#AppShellWindow {{
  background: {tokens.bg0};
}}

QFrame#Shell {{
  background: {tokens.bg0};
}}

QWidget[page_id] {{
  background: transparent;
}}

QFrame#TopBar {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 4}px;
}}

QFrame#RunStatusCard {{
  background: {surface_raised};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 6}px;
}}
QFrame#RunStatusCard:hover {{
  border-color: {focus_ring};
  background: {_alpha(tokens.panel2, 0.98)};
}}

QLabel#RunStatusTitle {{
  font-size: {d.font_size + 2}pt;
  font-weight: 700;
}}
QLabel#RunStatusDetail {{
  color: {tokens.text_muted};
  font-size: {d.font_size - 1}pt;
}}
QLabel#RunnerStatusChip {{
  background: {tokens.panel2};
  border: 1px solid {border_soft};
  border-radius: 999px;
  padding: 3px 10px;
  font-weight: 650;
}}
QLabel#RunnerStatusChip[kind="ok"] {{
  color: {tokens.ok};
}}
QLabel#RunnerStatusChip[kind="warn"] {{
  color: {tokens.warn};
}}
QLabel#RunnerStatusChip[kind="crit"] {{
  color: {tokens.crit};
}}
QLabel#RunnerStatusChip[kind="info"] {{
  color: {tokens.info};
}}

QFrame#SessionContext {{
  background: {_alpha(tokens.panel2, 0.92)};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 2}px;
}}

QScrollArea#PageScroll {{
  border: 0;
  background: transparent;
}}

QWidget#PageViewport {{
  background: transparent;
}}

QListWidget#MainNav {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 6}px;
  padding: 8px;
  outline: 0;
}}
QListWidget#MainNav::item {{
  height: {d.nav_item_height}px;
  padding: 0;
  border-radius: {d.corner_radius}px;
}}
QListWidget#MainNav::item:selected {{
  background: {accent_tint};
  border: 1px solid {focus_ring};
}}
QListWidget#MainNav::item:hover {{
  background: {surface_alt};
}}
QListWidget#MainNav::item:disabled {{
  color: {disabled_text};
}}

QListWidget#SettingsNav {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 6}px;
  padding: 8px;
  outline: 0;
}}
QListWidget#SettingsNav::item {{
  height: {max(d.nav_item_height, d.input_height + 6)}px;
  border-radius: {d.corner_radius}px;
}}
QListWidget#SettingsNav::item:selected {{
  background: {accent_tint};
  border: 1px solid {focus_ring};
}}
QListWidget#SettingsNav::item:hover {{
  background: {surface_alt};
}}
QListWidget#SettingsNav::item:disabled {{
  color: {disabled_text};
}}

QLabel#Title {{
  font-size: {d.font_size + 5}pt;
  font-weight: 650;
}}
QLabel#SubTitle {{
  color: {tokens.text_muted};
  font-size: {d.font_size - 1}pt;
}}
QLabel#SectionTitle {{
  font-size: {d.font_size + 1}pt;
  font-weight: 650;
}}
QLabel#CardTitle {{
  font-size: {d.font_size + 1}pt;
  font-weight: 650;
}}
QLabel#CardSubtitle {{
  color: {tokens.text_muted};
}}

QFrame#Card,
QFrame#Drawer,
QFrame#EmptyState,
QFrame#ConciergePanel,
QFrame#AccordionSection {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 2}px;
}}
QFrame#Card[elevation="1"],
QFrame#Drawer[elevation="1"],
QFrame#EmptyState[elevation="1"] {{
  background: {tokens.panel2};
}}
QFrame#Card[elevation="2"],
QFrame#Drawer[elevation="2"],
QFrame#EmptyState[elevation="2"] {{
  background: {_alpha(tokens.shadow2, 0.20)};
  border-color: {_alpha(tokens.border, 0.92)};
}}

QLabel#Pill {{
  background: {tokens.panel2};
  border: 1px solid {border_soft};
  border-radius: 999px;
  padding: 4px 10px;
  color: {text_soft};
}}
QLabel#Chip {{
  background: {tokens.panel2};
  border: 1px solid {border_soft};
  border-radius: 999px;
  padding: 4px 10px;
  color: {tokens.text_muted};
}}
QLabel#Chip[state="selected"] {{
  background: {accent_tint};
  border-color: {focus_ring};
  color: {tokens.text};
}}

QPushButton#PrimaryButton {{
  background: {tokens.accent};
  color: #FFFFFF;
  border: 1px solid {_alpha(tokens.accent_pressed, 0.9)};
  border-radius: {d.corner_radius}px;
  padding: 0 14px;
  min-height: {d.button_height}px;
  font-weight: 650;
}}
QPushButton#PrimaryButton:hover {{
  background: {tokens.accent_hover};
}}
QPushButton#PrimaryButton:pressed {{
  background: {tokens.accent_pressed};
}}

QPushButton#SoftButton,
QPushButton#SecondaryButton,
QToolButton#IconButton,
QToolButton#KebabMenuButton,
QToolButton#MoreButton {{
  background: {tokens.panel2};
  color: {tokens.text};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius}px;
  padding: 0 10px;
  min-height: {d.button_height}px;
}}
QPushButton#SoftButton:hover,
QPushButton#SecondaryButton:hover,
QToolButton#IconButton:hover,
QToolButton#KebabMenuButton:hover,
QToolButton#MoreButton:hover {{
  background: {surface_alt};
  border-color: {focus_ring};
}}
QPushButton#SoftButton:pressed,
QPushButton#SecondaryButton:pressed,
QToolButton#IconButton:pressed,
QToolButton#KebabMenuButton:pressed,
QToolButton#MoreButton:pressed {{
  background: {accent_tint};
}}
QPushButton#SoftButton:checked,
QPushButton#SecondaryButton:checked {{
  background: {accent_tint_strong};
  border-color: {focus_ring};
}}

QPushButton#TextButton {{
  background: transparent;
  color: {tokens.info};
  border: 1px solid transparent;
  border-radius: {d.corner_radius}px;
  padding: 0 10px;
  min-height: {d.button_height}px;
  font-weight: 600;
}}
QPushButton#TextButton:hover {{
  background: {accent_tint};
}}
QPushButton#TextButton:pressed {{
  background: {accent_tint_strong};
}}

QPushButton#PrimaryButton:focus,
QPushButton#SoftButton:focus,
QPushButton#SecondaryButton:focus,
QPushButton#TextButton:focus,
QToolButton#IconButton:focus,
QToolButton#KebabMenuButton:focus,
QToolButton#MoreButton:focus,
QLineEdit#SearchInput:focus,
QLineEdit:focus,
QComboBox:focus,
QListWidget:focus,
QListWidget#MainNav:focus,
QListWidget#SettingsNav:focus,
QTreeWidget:focus,
QTextEdit:focus,
QPlainTextEdit:focus {{
  border: 1px solid {focus_ring};
}}

QToolButton#IconButton {{
  min-width: {d.button_height}px;
  max-width: {d.button_height + 8}px;
  icon-size: {d.icon_size}px;
  padding: 0;
}}
QToolButton#KebabMenuButton {{
  min-width: {d.button_height}px;
  max-width: {d.button_height}px;
  font-size: {d.font_size + 1}pt;
  icon-size: {d.icon_size}px;
  padding: 0;
}}
QToolButton#MoreButton {{
  min-width: {max(124, d.button_height + 40)}px;
  padding: 0 10px;
}}

QFrame#ResultRow {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius}px;
}}
QLabel#ResultTitle {{
  font-weight: 650;
}}
QLabel#ResultDetail {{
  color: {tokens.text_muted};
}}

QLabel#TagOK, QLabel#BadgeOK {{
  color: {tokens.ok};
  font-weight: 700;
}}
QLabel#TagWARN, QLabel#BadgeWARN {{
  color: {tokens.warn};
  font-weight: 700;
}}
QLabel#TagCRIT, QLabel#BadgeCRIT {{
  color: {tokens.crit};
  font-weight: 700;
}}
QLabel#TagINFO, QLabel#BadgeINFO {{
  color: {tokens.info};
  font-weight: 700;
}}
QLabel#BadgeRiskSafe {{
  color: {tokens.ok};
  font-weight: 700;
}}
QLabel#BadgeRiskAdmin {{
  color: {tokens.warn};
  font-weight: 700;
}}
QLabel#BadgeRiskAdvanced {{
  color: {tokens.crit};
  font-weight: 700;
}}

QLineEdit#SearchInput,
QLineEdit,
QComboBox,
QTextEdit,
QPlainTextEdit,
QTreeWidget {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius}px;
}}
QAbstractScrollArea {{
  background: {tokens.panel};
}}
QLineEdit#SearchInput,
QLineEdit {{
  min-height: {d.input_height}px;
  padding: 0 12px;
}}
QComboBox {{
  min-height: {d.input_height}px;
  padding: 0 10px;
}}
QTextEdit,
QPlainTextEdit {{
  padding: 8px;
}}

QCheckBox,
QRadioButton {{
  spacing: 8px;
}}
QCheckBox::indicator,
QRadioButton::indicator {{
  width: 16px;
  height: 16px;
}}
QCheckBox::indicator:unchecked,
QRadioButton::indicator:unchecked {{
  border: 1px solid {_alpha(tokens.border, 0.96)};
  background: {tokens.panel};
  border-radius: 8px;
}}
QCheckBox::indicator:checked,
QRadioButton::indicator:checked {{
  border: 1px solid {focus_ring};
  background: {tokens.accent};
  border-radius: 8px;
}}

QSlider::groove:horizontal {{
  height: 6px;
  background: {tokens.panel2};
  border: 1px solid {border_soft};
  border-radius: 3px;
}}
QSlider::sub-page:horizontal {{
  background: {_alpha(tokens.accent, 0.70)};
  border-radius: 3px;
}}
QSlider::handle:horizontal {{
  width: 14px;
  margin: -6px 0;
  border-radius: 7px;
  background: {tokens.accent};
  border: 1px solid {_alpha(tokens.accent_pressed, 0.95)};
}}
QSlider::handle:horizontal:hover {{
  background: {tokens.accent_hover};
}}
QSlider::handle:horizontal:pressed {{
  background: {tokens.accent_pressed};
}}

QListWidget {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 2}px;
  outline: 0;
}}
QListWidget::item {{
  border-bottom: 1px solid {border_subtle};
  margin: 0;
  padding: 0;
}}
QListWidget::item:selected {{
  background: {accent_tint};
}}

QTreeWidget::item,
QTreeView::item {{
  min-height: {max(24, d.list_row_height - 26)}px;
  padding: 2px 4px;
}}
QHeaderView::section {{
  background: {tokens.panel2};
  border: 0;
  border-bottom: 1px solid {border_soft};
  padding: 4px 8px;
  color: {tokens.text_muted};
}}

QTabBar::tab {{
  min-height: {max(24, d.input_height - 6)}px;
  padding: 0 12px;
  margin-right: 4px;
  background: {tokens.panel2};
  border: 1px solid {border_soft};
  border-radius: {max(6, d.corner_radius - 4)}px;
}}
QTabBar::tab:selected {{
  background: {accent_tint};
  border-color: {focus_ring};
}}
QTabBar::tab:hover {{
  background: {surface_alt};
}}

QScrollBar:vertical {{
  background: transparent;
  width: {scroll_size}px;
  margin: 2px;
}}
QScrollBar::handle:vertical {{
  background: {_alpha(tokens.border, 0.85)};
  min-height: 28px;
  border-radius: {max(5, scroll_size // 2)}px;
}}
QScrollBar::handle:vertical:hover {{
  background: {_alpha(tokens.accent, 0.72)};
}}
QScrollBar::handle:vertical:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.86)};
}}

QScrollBar:horizontal {{
  background: transparent;
  height: {scroll_size}px;
  margin: 2px;
}}
QScrollBar::handle:horizontal {{
  background: {_alpha(tokens.border, 0.85)};
  min-width: 28px;
  border-radius: {max(5, scroll_size // 2)}px;
}}
QScrollBar::handle:horizontal:hover {{
  background: {_alpha(tokens.accent, 0.72)};
}}
QScrollBar::handle:horizontal:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.86)};
}}

QScrollBar::add-line:vertical,
QScrollBar::sub-line:vertical,
QScrollBar::add-line:horizontal,
QScrollBar::sub-line:horizontal {{
  width: 0;
  height: 0;
  background: transparent;
  border: 0;
}}
QScrollBar::add-page:vertical,
QScrollBar::sub-page:vertical,
QScrollBar::add-page:horizontal,
QScrollBar::sub-page:horizontal {{
  background: transparent;
}}

QFrame#RowBase {{
  background: transparent;
  border: 1px solid transparent;
  border-radius: {d.corner_radius}px;
}}
QFrame#RowBase[state="hover"] {{
  background: {surface_alt};
  border-color: {_alpha(tokens.border, 0.86)};
}}
QFrame#RowBase[state="selected"] {{
  background: {accent_tint};
  border-color: {focus_ring};
}}
QFrame#RowBase:focus {{
  border: 1px solid {focus_ring};
}}
QLabel#RowTitle {{
  font-weight: 650;
}}
QLabel#RowSubtitle {{
  color: {tokens.text_muted};
}}
QLabel#RowIconSlot {{
  background: {tokens.panel2};
  border: 1px solid {border_subtle};
  border-radius: {max(8, d.corner_radius - 3)}px;
}}

QLabel#Badge {{
  border-radius: 999px;
  border: 1px solid {border_soft};
  padding: 2px 9px;
  min-height: 18px;
  background: {tokens.panel2};
}}

QFrame#Toast {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius}px;
}}
QFrame#Skeleton {{
  background: {tokens.panel2};
  border: 1px solid {border_subtle};
  border-radius: {max(8, d.corner_radius - 2)}px;
}}

QSplitter::handle:horizontal {{
  width: {splitter_size}px;
  margin: 4px 0;
  border-radius: {max(3, splitter_size // 2)}px;
  background: {_alpha(tokens.border, 0.56)};
}}
QSplitter::handle:horizontal:hover {{
  background: {_alpha(tokens.accent, 0.48)};
}}
QSplitter::handle:horizontal:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.56)};
}}
QSplitter::handle:vertical {{
  height: {splitter_size}px;
  margin: 0 4px;
  border-radius: {max(3, splitter_size // 2)}px;
  background: {_alpha(tokens.border, 0.56)};
}}
QSplitter::handle:vertical:hover {{
  background: {_alpha(tokens.accent, 0.48)};
}}
QSplitter::handle:vertical:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.56)};
}}

QFrame#AccordionHeader {{
  background: {tokens.panel2};
  border-radius: {d.corner_radius}px;
  border: 1px solid {border_soft};
}}

QLabel#ModeFlag {{
  color: {tokens.text_muted};
}}

QMenu {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius}px;
  padding: 4px;
}}
QMenu::item {{
  padding: 6px 12px;
  border-radius: {max(4, d.corner_radius - 6)}px;
}}
QMenu::item:selected {{
  background: {accent_tint};
}}

QToolTip {{
  background: {tokens.panel};
  color: {tokens.text};
  border: 1px solid {border_soft};
  border-radius: {max(6, d.corner_radius - 4)}px;
  padding: 6px 8px;
}}

QFrame#GlobalSearchPopup {{
  background: {tokens.panel};
  border: 1px solid {border_soft};
  border-radius: {d.corner_radius + 2}px;
}}
QLabel#GlobalSearchEmpty {{
  color: {tokens.text_muted};
  padding: 8px 10px;
}}
QListWidget#GlobalSearchList {{
  background: transparent;
  border: 0;
  border-radius: {d.corner_radius}px;
  outline: 0;
}}
QListWidget#GlobalSearchList::item {{
  border-radius: {max(8, d.corner_radius - 4)}px;
  padding: 8px 10px;
  margin: 1px 0;
}}
QListWidget#GlobalSearchList::item:disabled {{
  color: {tokens.text_muted};
  font-weight: 650;
  background: transparent;
  border: 0;
  padding: 6px 2px;
}}
QListWidget#GlobalSearchList::item:selected {{
  background: {accent_tint};
  border: 1px solid {focus_ring};
}}
QListWidget#GlobalSearchList::item:hover {{
  background: {surface_alt};
}}

QWidget:disabled {{
  color: {disabled_text};
}}
QPushButton:disabled,
QToolButton:disabled,
QLineEdit:disabled,
QComboBox:disabled {{
  background: {disabled_bg};
  color: {disabled_text};
  border: 1px solid {border_subtle};
}}
"""
