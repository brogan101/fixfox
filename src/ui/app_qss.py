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
    density_key = normalize_density(density)
    d = resolve_density_tokens(density_key)
    scroll_size = 12 if density_key == "comfortable" else 10
    splitter_size = 8 if density_key == "comfortable" else 6
    outline = _alpha(tokens.accent, 0.45)
    selection = _alpha(tokens.accent, 0.20)
    panel_soft = _alpha(tokens.panel2, 0.84)
    text_soft = _alpha(tokens.text, 0.78)
    subtle_line = _alpha(tokens.border, 0.50)
    shadow_1 = _alpha(tokens.shadow1, 0.28)
    shadow_2 = _alpha(tokens.shadow2, 0.50)
    return f"""
QWidget {{
  color: {tokens.text};
  background: transparent;
  font-family: {BASE_FONT_FAMILY};
  font-size: {d.font_size}pt;
}}
QMainWindow {{
  background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 {tokens.bg0}, stop:1 {tokens.bg1});
}}
QMainWindow#AppShellWindow {{
  background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 {tokens.bg0}, stop:1 {tokens.bg1});
}}
QFrame#Shell {{
  background: {_alpha(tokens.bg0, 0.76)};
}}
QWidget[page_id] {{
  background: transparent;
}}
QFrame#TopBar {{
  background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 {panel_soft}, stop:1 {_alpha(tokens.panel, 0.86)});
  border: 1px solid {_alpha(tokens.shadow1, 0.30)};
  border-radius: {d.corner_radius + 3}px;
}}
QFrame#RunStatusCard {{
  background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 {_alpha(tokens.panel2, 0.92)}, stop:1 {_alpha(tokens.panel, 0.78)});
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius + 2}px;
}}
QFrame#RunStatusCard:hover {{
  border-color: {outline};
  background: qlineargradient(x1:0, y1:0, x2:1, y2:0, stop:0 {_alpha(tokens.accent, 0.12)}, stop:1 {_alpha(tokens.panel, 0.82)});
}}
QLabel#RunStatusTitle {{
  font-size: {d.font_size + 2}pt;
  font-weight: 700;
}}
QLabel#RunStatusDetail {{
  color: {tokens.text_muted};
  font-size: {d.font_size - 1}pt;
}}
QFrame#SessionContext {{
  background: {_alpha(tokens.accent, 0.11)};
  border: 1px solid {_alpha(tokens.accent, 0.44)};
  border-radius: {d.corner_radius}px;
}}
QScrollArea#PageScroll {{
  border: 0;
  background: transparent;
}}
QWidget#PageViewport {{
  background: transparent;
}}
QListWidget#Nav {{
  background: {_alpha(tokens.panel2, 0.92)};
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius + 4}px;
  padding: 8px;
  outline: 0;
}}
QListWidget#Nav::item {{
  height: {d.nav_item_height}px;
  padding: 0;
  border-radius: {d.corner_radius}px;
}}
QListWidget#Nav::item:selected {{
  background: {selection};
  border: 1px solid {outline};
}}
QLabel#Title {{
  font-size: {d.font_size + 9}pt;
  font-weight: 700;
}}
QLabel#SubTitle {{
  color: {tokens.text_muted};
  font-size: {d.font_size - 1}pt;
}}
QLabel#SectionTitle {{
  font-size: {d.font_size + 1}pt;
  font-weight: 700;
}}
QLabel#CardTitle {{
  font-size: {d.font_size + 1}pt;
  font-weight: 650;
}}
QLabel#CardSubtitle {{
  color: {tokens.text_muted};
}}
QFrame#Card, QFrame#Drawer, QFrame#EmptyState, QFrame#ConciergePanel, QFrame#AccordionSection {{
  background: qlineargradient(x1:0, y1:0, x2:0, y2:1, stop:0 {_alpha(tokens.panel, 0.90)}, stop:1 {_alpha(tokens.panel2, 0.82)});
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius + 4}px;
}}
QFrame#CardHeader {{
  background: transparent;
}}
QLabel#Pill {{
  background: {_alpha(tokens.panel2, 0.95)};
  border: 1px solid {subtle_line};
  border-radius: 999px;
  padding: 4px 10px;
  color: {text_soft};
}}
QPushButton#PrimaryButton {{
  background: {tokens.accent};
  color: {tokens.bg0};
  border: 0;
  border-radius: {d.corner_radius}px;
  padding: 0 14px;
  min-height: {d.button_height}px;
  font-weight: 700;
}}
QPushButton#PrimaryButton:hover {{ background: {tokens.accent_hover}; }}
QPushButton#PrimaryButton:pressed {{ background: {tokens.accent_pressed}; }}
QPushButton#SoftButton,
QToolButton#IconButton,
QToolButton#KebabMenuButton,
QToolButton#MoreButton {{
  background: {_alpha(tokens.panel2, 0.88)};
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius}px;
  padding: 0 10px;
  min-height: {d.button_height}px;
}}
QPushButton#SoftButton:hover,
QToolButton#IconButton:hover,
QToolButton#KebabMenuButton:hover,
QToolButton#MoreButton:hover {{
  background: {_alpha(tokens.accent, 0.16)};
  border-color: {outline};
}}
QPushButton#SoftButton:pressed,
QToolButton#IconButton:pressed,
QToolButton#KebabMenuButton:pressed,
QToolButton#MoreButton:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.34)};
}}
QPushButton#SoftButton:checked {{
  background: {_alpha(tokens.accent, 0.22)};
  border-color: {outline};
}}
QPushButton#PrimaryButton:focus,
QPushButton#SoftButton:focus,
QToolButton#IconButton:focus,
QToolButton#KebabMenuButton:focus,
QToolButton#MoreButton:focus,
QLineEdit#SearchInput:focus,
QComboBox:focus,
QListWidget:focus,
QTreeWidget:focus,
QTextEdit:focus,
QPlainTextEdit:focus {{
  border: 1px solid {outline};
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
  font-size: {d.font_size + 2}pt;
  icon-size: {d.icon_size}px;
  padding: 0;
}}
QToolButton#MoreButton {{
  min-width: {max(120, d.button_height + 36)}px;
  padding: 0 10px;
}}
QFrame#ResultRow {{
  background: {_alpha(tokens.panel2, 0.92)};
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius}px;
}}
QLabel#ResultTitle {{ font-weight: 650; }}
QLabel#ResultDetail {{ color: {tokens.text_muted}; }}
QLabel#TagOK, QLabel#BadgeOK {{ color: {tokens.ok}; font-weight: 700; }}
QLabel#TagWARN, QLabel#BadgeWARN {{ color: {tokens.warn}; font-weight: 700; }}
QLabel#TagCRIT, QLabel#BadgeCRIT {{ color: {tokens.crit}; font-weight: 700; }}
QLabel#TagINFO, QLabel#BadgeINFO {{ color: {tokens.info}; font-weight: 700; }}
QLabel#BadgeRiskSafe {{ color: {tokens.ok}; font-weight: 700; }}
QLabel#BadgeRiskAdmin {{ color: {tokens.warn}; font-weight: 700; }}
QLabel#BadgeRiskAdvanced {{ color: {tokens.crit}; font-weight: 700; }}
QLineEdit#SearchInput, QComboBox, QTextEdit, QPlainTextEdit, QTreeWidget {{
  background: {_alpha(tokens.panel2, 0.86)};
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius}px;
}}
QAbstractScrollArea {{
  background: {_alpha(tokens.panel2, 0.84)};
}}
QLineEdit#SearchInput {{
  min-height: {d.input_height}px;
  padding: 0 10px;
}}
QComboBox {{
  min-height: {d.input_height}px;
  padding: 0 8px;
}}
QTextEdit {{
  padding: 8px;
}}
QPlainTextEdit {{
  padding: 8px;
}}
QListWidget {{
  background: {_alpha(tokens.panel2, 0.84)};
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius + 2}px;
  outline: 0;
}}
QListWidget::item {{
  border-bottom: 1px solid {_alpha(tokens.border, 0.36)};
  margin: 0;
  padding: 0;
}}
QListWidget::item:selected {{
  background: {selection};
}}
QTreeWidget::item,
QTreeView::item {{
  min-height: {max(24, d.list_row_height - 24)}px;
  padding: 2px 4px;
}}
QHeaderView::section {{
  background: {_alpha(tokens.panel2, 0.90)};
  border: 0;
  border-bottom: 1px solid {_alpha(tokens.border, 0.46)};
  padding: 4px 8px;
  color: {tokens.text_muted};
}}
QTabBar::tab {{
  min-height: {max(24, d.input_height - 6)}px;
  padding: 0 10px;
  margin-right: 4px;
  background: {_alpha(tokens.panel2, 0.78)};
  border: 1px solid {_alpha(tokens.border, 0.52)};
  border-radius: {max(6, d.corner_radius - 4)}px;
}}
QTabBar::tab:selected {{
  background: {selection};
  border-color: {outline};
}}
QTabBar::tab:hover {{
  background: {_alpha(tokens.accent, 0.14)};
}}
QScrollBar:vertical {{
  background: {_alpha(tokens.panel2, 0.74)};
  width: {scroll_size}px;
  margin: 2px;
  border: 1px solid {_alpha(tokens.border, 0.52)};
  border-radius: {max(4, scroll_size // 2)}px;
}}
QScrollBar::handle:vertical {{
  background: {_alpha(tokens.border, 0.88)};
  min-height: 26px;
  border-radius: {max(4, scroll_size // 2)}px;
}}
QScrollBar::handle:vertical:hover {{
  background: {_alpha(tokens.accent, 0.78)};
}}
QScrollBar::handle:vertical:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.88)};
}}
QScrollBar:horizontal {{
  background: {_alpha(tokens.panel2, 0.74)};
  height: {scroll_size}px;
  margin: 2px;
  border: 1px solid {_alpha(tokens.border, 0.52)};
  border-radius: {max(4, scroll_size // 2)}px;
}}
QScrollBar::handle:horizontal {{
  background: {_alpha(tokens.border, 0.88)};
  min-width: 26px;
  border-radius: {max(4, scroll_size // 2)}px;
}}
QScrollBar::handle:horizontal:hover {{
  background: {_alpha(tokens.accent, 0.78)};
}}
QScrollBar::handle:horizontal:pressed {{
  background: {_alpha(tokens.accent_pressed, 0.88)};
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
  background: {_alpha(tokens.accent, 0.08)};
  border-color: {_alpha(tokens.accent, 0.25)};
}}
QFrame#RowBase[state="selected"] {{
  background: {selection};
  border-color: {outline};
}}
QFrame#RowBase:focus {{
  border: 1px solid {outline};
}}
QLabel#RowTitle {{
  font-weight: 650;
}}
QLabel#RowSubtitle {{
  color: {tokens.text_muted};
}}
QLabel#Badge {{
  border-radius: 999px;
  border: 1px solid {_alpha(tokens.border, 0.62)};
  padding: 2px 9px;
  min-height: 18px;
  background: {_alpha(tokens.panel, 0.90)};
}}
QFrame#Toast {{
  background: {_alpha(tokens.panel2, 0.98)};
  border: 1px solid {_alpha(tokens.shadow1, 0.35)};
  border-radius: {d.corner_radius}px;
}}
QFrame#Skeleton {{
  background: {_alpha(tokens.panel2, 0.94)};
  border: 1px solid {subtle_line};
  border-radius: {d.corner_radius - 2}px;
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
  background: {_alpha(tokens.panel2, 0.92)};
  border-radius: {d.corner_radius}px;
  border: 1px solid {_alpha(tokens.border, 0.56)};
}}
QLabel#ModeFlag {{
  color: {tokens.text_muted};
}}
QMenu {{
  background: {_alpha(tokens.panel2, 0.97)};
  border: 1px solid {shadow_1};
  border-radius: {d.corner_radius}px;
}}
QMenu::item {{
  padding: 6px 12px;
  border-radius: {d.corner_radius - 4}px;
}}
QMenu::item:selected {{
  background: {selection};
}}
"""
