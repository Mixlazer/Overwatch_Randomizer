import tkinter as tk
from tkinter import filedialog, messagebox, ttk
import json
import random
import os
import struct
import subprocess
import sys
import tempfile
import threading

from ocr_helper import extract_stats_from_rows, parse_scoreboard_screenshot as parse_scoreboard_in_process

# English roles
roles = ['Tank', 'Damage', 'Support']

# Full roster by role
characters_by_role_full = {
    'Tank': [
        'D.Va', 'Domina', 'Doomfist', 'Hazard', 'Junker Queen', 'Mauga', 'Orisa',
        'Ramattra', 'Reinhardt', 'Roadhog', 'Sigma', 'Winston', 'Wrecking Ball', 'Zarya'
    ],
    'Damage': [
        'Anran', 'Ashe', 'Bastion', 'Cassidy', 'Echo', 'Emre', 'Freja', 'Genji',
        'Hanzo', 'Junkrat', 'Mei', 'Pharah', 'Reaper', 'Shion', 'Sierra',
        'Sojourn', 'Soldier: 76', 'Sombra', 'Symmetra', 'Torbjörn', 'Tracer',
        'Vendetta', 'Venture', 'Widowmaker'
    ],
    'Support': [
        'Ana', 'Baptiste', 'Brigitte', 'Illari', 'Jetpack Cat', 'Juno', 'Kiriko',
        'Lifeweaver', 'Lúcio', 'Mercy', 'Mizuki', 'Moira', 'Wuyang', 'Zenyatta'
    ]
}

# Limited Stadium roster
stadium_characters = [
    'D.Va', 'Doomfist', 'Hazard', 'Junker Queen', 'Orisa', 'Ramattra',
    'Reinhardt', 'Sigma', 'Winston', 'Zarya',
    'Ashe', 'Cassidy', 'Freja', 'Genji', 'Junkrat', 'Mei', 'Pharah', 'Reaper',
    'Sojourn', 'Soldier: 76', 'Torbjörn', 'Tracer', 'Vendetta',
    'Ana', 'Brigitte', 'Jetpack Cat', 'Juno', 'Kiriko', 'Lúcio', 'Mercy',
    'Moira', 'Wuyang', 'Zenyatta'
]

DEFAULT_GAME_ROLES = ('Tank', 'Damage', 'Damage', 'Support', 'Support')
STAT_KEYS = ('elims', 'objective', 'damage', 'healing', 'deaths')


# Modes to count mapping
mode_to_count = {
    '5v5': 5,
    'Open': 6,
    'Stadium': 5
}

# Role limits per mode
role_constraints = {
    '5v5': {'Tank': 1, 'Damage': 2, 'Support': 2},
    'Stadium': {'Tank': 1, 'Damage': 2, 'Support': 2},
    'Open': {'Tank': 2, 'Damage': 6, 'Support': 6}
}

BASE_DIR = os.path.dirname(__file__)


def icon_path(character):
    name = character.replace(': ', '').replace(':', '').replace(' ', '_')
    path = os.path.join(BASE_DIR, 'assets', f"Icon-{name}.png")
    if os.path.exists(path):
        return path
    return None


def png_size(path):
    with open(path, 'rb') as file:
        assert file.read(8) == b'\x89PNG\r\n\x1a\n'
        assert file.read(4) == b'\x00\x00\x00\r'
        assert file.read(4) == b'IHDR'
        return struct.unpack('>II', file.read(8))


def game_hero_pool(role, mode):
    pool = characters_by_role_full[role]
    if mode == 'Stadium':
        pool = [hero for hero in pool if hero in stadium_characters]
    return pool


def random_game_roles(count):
    assigned = list(DEFAULT_GAME_ROLES)
    random.shuffle(assigned)
    if count > len(assigned):
        assigned.extend(random.choices(roles, k=count - len(assigned)))
    return assigned[:count]


def all_game_heroes(mode):
    return [(role, hero) for role in roles for hero in game_hero_pool(role, mode)]


def hero_role(hero):
    return next(role for role in roles if hero in characters_by_role_full[role])


def random_game_hero(role, mode, current=None, excluded=()):
    pool = [hero for hero in game_hero_pool(role, mode) if hero != current and hero not in excluded]
    if not pool:
        pool = [hero for hero in game_hero_pool(role, mode) if hero != current]
    return random.choice(pool)


def random_game_team(count, mode):
    team = []
    used = set()
    for role in random_game_roles(count):
        hero = random_game_hero(role, mode, excluded=used)
        team.append((role, hero))
        used.add(hero)
    return team


def random_game_roster(mode):
    return [hero for _role, hero in random_game_team(5, mode)]


def parse_stat_value(value, objective=False):
    text = str(value).strip().replace(' ', '').replace(',', '')
    if objective and ':' in text:
        minutes, seconds = text.split(':', 1)
        if not minutes.isdigit() or not seconds.isdigit() or int(seconds) >= 60:
            raise ValueError(value)
        return int(minutes) * 60 + int(seconds)
    if not text.isdigit():
        raise ValueError(value)
    return int(text)


def calculate_round_points(elims, objective, damage, healing, deaths):
    return 55 + elims * 12 + objective // 10 * 10 + damage // 400 + healing // 400 - deaths * 5


def transfer_received(amount):
    return (amount + 1) // 2


def parse_scoreboard_screenshot(path, player_names):
    if not getattr(sys, 'frozen', False):
        return parse_scoreboard_in_process(path, player_names)

    helper = os.path.join(os.path.dirname(sys.executable), 'OverwatchRandomizerOCR.exe')
    if not os.path.exists(helper):
        raise RuntimeError('OverwatchRandomizerOCR.exe is missing. Reinstall the application.')
    with tempfile.TemporaryDirectory() as temp_dir:
        output_path = os.path.join(temp_dir, 'ocr.json')
        process = subprocess.run(
            [helper, path, json.dumps(player_names, ensure_ascii=False), output_path],
            creationflags=getattr(subprocess, 'CREATE_NO_WINDOW', 0),
            check=False,
            timeout=180,
        )
        if not os.path.exists(output_path):
            raise RuntimeError(f'LiteParse helper stopped with code {process.returncode}.')
        with open(output_path, encoding='utf-8') as output:
            payload = json.load(output)
    if 'error' in payload:
        raise RuntimeError(payload['error'])
    if process.returncode:
        raise RuntimeError(f'LiteParse helper stopped with code {process.returncode}.')
    return {int(index): stats for index, stats in payload['results'].items()}, payload['text']


def self_check():
    all_characters = {ch for roster in characters_by_role_full.values() for ch in roster}
    assert set(stadium_characters) <= all_characters
    assert all(icon_path(ch) for ch in all_characters)
    assert all(png_size(icon_path(ch)) == (50, 50) for ch in all_characters)
    assert calculate_round_points(2, 25, 800, 799, 3) == 87
    assert transfer_received(50) == 25
    assert transfer_received(51) == 26
    assert extract_stats_from_rows(['Mixlazer 10 1:20 2,400 800 5'], ['Mixlazer'])[0] == {
        'elims': 10, 'objective': 80, 'damage': 2400, 'healing': 800, 'deaths': 5
    }
    for mode in ('Standard', 'Stadium'):
        roster = random_game_roster(mode)
        assert sorted(hero_role(hero) for hero in roster) == ['Damage', 'Damage', 'Support', 'Support', 'Tank']
        assert len(set(roster)) == 5
        if mode == 'Stadium':
            assert set(roster) <= set(stadium_characters)
    print(f"OK: {len(all_characters)} heroes, {len(stadium_characters)} stadium heroes, game rules")


def lower_process_priority():
    if os.name != 'nt':
        return
    try:
        import ctypes

        ctypes.windll.kernel32.SetPriorityClass(
            ctypes.windll.kernel32.GetCurrentProcess(),
            0x00004000,  # BELOW_NORMAL_PRIORITY_CLASS
        )
    except Exception:
        pass

class OverwatchRandomizerApp:
    def __init__(self, root):
        self.root = root
        self.root.title("Overwatch Randomizer")

        self.mode_var = tk.StringVar(value='5v5')
        self.custom_count_var = tk.IntVar(value=1)
        self.custom_count_var.trace_add('write', self.on_custom_count_change)
        self.roles_labels = []
        self.character_labels = []
        self.avatar_labels = []
        self.avatar_images = []
        self.photo_cache = {}

        self.game_players = []
        self.game_rows = []
        self.active_game_pool = 'Standard'
        self.emergency_available = 1
        self.ocr_running = False
        self.game_player_count_var = tk.IntVar(value=5)
        self.game_pool_var = tk.StringVar(value='Standard')
        self.game_status_var = tk.StringVar()
        self.ocr_status_var = tk.StringVar(value='LiteParse ready')

        self.notebook = ttk.Notebook(self.root)
        self.random_tab = ttk.Frame(self.notebook, padding=6)
        self.game_tab = ttk.Frame(self.notebook, padding=6)
        self.notebook.add(self.random_tab, text='Randomizer')
        self.notebook.add(self.game_tab, text='Game')
        self.notebook.pack(fill='both', expand=True)
        self.notebook.bind('<<NotebookTabChanged>>', self.on_tab_change)

        self.create_widgets()
        self.create_game_widgets()
        self.refresh()
        self.start_new_game(initial=True)
        self.root.after_idle(self.resize_to_current_tab)

    def create_widgets(self):
        parent = self.random_tab
        ttk.Label(parent, text="Mode:").grid(row=0, column=0, sticky='w')
        options = list(mode_to_count.keys()) + ['Custom']
        self.option_menu = ttk.OptionMenu(
            parent, self.mode_var, self.mode_var.get(), *options, command=self.on_mode_change
        )
        self.option_menu.grid(row=0, column=1, sticky='w')

        ttk.Label(parent, text="Count (1-10):").grid(row=0, column=2, sticky='w')
        self.custom_label = parent.grid_slaves(row=0, column=2)[0]
        self.custom_entry = ttk.Entry(parent, width=5, textvariable=self.custom_count_var)
        self.custom_entry.grid(row=0, column=3, sticky='w')
        self.custom_label.grid_remove()
        self.custom_entry.grid_remove()

        self.role_frame = tk.Frame(parent)
        self.role_frame.grid(row=1, column=0, columnspan=4)

        self.button_roles = ttk.Button(parent, text="Generate Roles", command=self.generate_roles)
        self.button_roles.grid(row=2, column=0, pady=5, sticky='w')
        self.button_characters = ttk.Button(parent, text="Generate Characters", command=self.generate_characters)
        self.button_characters.grid(row=2, column=1, pady=5, sticky='w')

    def create_game_widgets(self):
        setup = ttk.Frame(self.game_tab)
        setup.pack(fill='x', pady=(0, 6))
        ttk.Label(setup, text='Players:').pack(side='left')
        ttk.Spinbox(
            setup, from_=1, to=10, width=4, textvariable=self.game_player_count_var,
            command=self.start_new_game,
        ).pack(side='left', padx=(4, 12))
        ttk.Label(setup, text='Hero pool:').pack(side='left')
        pool_combo = ttk.Combobox(
            setup, width=10, state='readonly', textvariable=self.game_pool_var,
            values=('Standard', 'Stadium')
        )
        pool_combo.pack(side='left', padx=(4, 12))
        ttk.Button(setup, text='Apply', command=self.start_new_game).pack(side='left')

        list_frame = ttk.Frame(self.game_tab)
        list_frame.pack(fill='both', expand=True)
        self.game_canvas = tk.Canvas(list_frame, width=860, height=335, highlightthickness=0, borderwidth=0)
        scrollbar = ttk.Scrollbar(list_frame, orient='vertical', command=self.game_canvas.yview)
        self.game_canvas.configure(yscrollcommand=scrollbar.set)
        self.game_canvas.pack(side='left', fill='both', expand=True)
        scrollbar.pack(side='right', fill='y')
        self.game_rows_frame = ttk.Frame(self.game_canvas)
        self.game_canvas_window = self.game_canvas.create_window(
            (0, 0), window=self.game_rows_frame, anchor='nw'
        )
        self.game_rows_frame.bind(
            '<Configure>', lambda _event: self.game_canvas.configure(scrollregion=self.game_canvas.bbox('all'))
        )
        self.game_canvas.bind(
            '<Configure>', lambda event: self.game_canvas.itemconfigure(self.game_canvas_window, width=event.width)
        )

        footer = ttk.Frame(self.game_tab)
        footer.pack(fill='x', pady=(8, 0))
        self.emergency_button = ttk.Button(footer, text='Emergency all (1/1)', command=self.emergency_reroll)
        self.emergency_button.pack(side='left')
        self.ocr_button = ttk.Button(footer, text='Load screenshot', command=self.load_scoreboard)
        self.ocr_button.pack(side='left', padx=8)
        ttk.Button(footer, text='New round', command=self.finish_round).pack(side='left')
        ttk.Label(footer, textvariable=self.game_status_var).pack(side='left', padx=12)
        ttk.Label(self.game_tab, textvariable=self.ocr_status_var).pack(fill='x', pady=(4, 0))

    def on_tab_change(self, _event=None):
        self.root.after_idle(self.resize_to_current_tab)

    def resize_to_current_tab(self):
        selected = self.notebook.select()
        if not selected:
            return
        tab = self.root.nametowidget(selected)
        tab.update_idletasks()
        self.notebook.configure(
            width=max(310, tab.winfo_reqwidth()),
            height=max(140, tab.winfo_reqheight()),
        )
        self.root.update_idletasks()
        self.root.geometry(f'{self.notebook.winfo_reqwidth()}x{self.notebook.winfo_reqheight()}')

    def on_mode_change(self, mode):
        if mode == 'Custom':
            self.custom_label.grid()
            self.custom_entry.grid()
        else:
            self.custom_label.grid_remove()
            self.custom_entry.grid_remove()
        self.refresh()

    def on_custom_count_change(self, *args):
        if self.mode_var.get() == 'Custom':
            self.refresh()

    def refresh(self):
        self.update_count()
        self.generate_roles()
        self.generate_characters()
        self.root.after_idle(self.resize_to_current_tab)

    def update_count(self):
        mode = self.mode_var.get()
        count = 1
        if mode == 'Custom':
            try:
                count = int(self.custom_count_var.get())
            except (ValueError, tk.TclError):
                count = 1
            count = max(1, min(10, count))
        else:
            count = mode_to_count.get(mode, 5)

        for widget in self.role_frame.winfo_children():
            widget.destroy()
        self.roles_labels.clear()
        self.character_labels.clear()
        self.avatar_labels.clear()
        self.avatar_images.clear()

        for i in range(count):
            lbl_role = ttk.Label(self.role_frame, text="Role", width=8)
            lbl_role.grid(row=i, column=0, padx=5, pady=5)
            self.roles_labels.append(lbl_role)
            lbl_char = ttk.Label(self.role_frame, text="Character", width=16)
            lbl_char.grid(row=i, column=1, padx=5, pady=5)
            self.character_labels.append(lbl_char)
            lbl_img = tk.Label(self.role_frame, width=50, height=50)
            lbl_img.grid(row=i, column=2, padx=5, pady=5)
            self.avatar_labels.append(lbl_img)
            self.avatar_images.append(None)

    def generate_roles(self):
        mode = self.mode_var.get()
        count = len(self.roles_labels)
        limits = role_constraints.get(mode, {}).copy()
        available_roles = []
        if limits:
            for role, qty in limits.items():
                available_roles.extend([role] * qty)
        if count > len(available_roles):
            available_roles.extend(random.choices(roles, k=count - len(available_roles)))
        random.shuffle(available_roles)
        self.generated_roles = available_roles[:count]
        for i, role in enumerate(self.generated_roles):
            self.roles_labels[i].config(text=role)

    def generate_characters(self):
        selected_characters = set()
        mode = self.mode_var.get()
        for i, lbl in enumerate(self.roles_labels):
            role = lbl.cget("text")
            if mode == 'Stadium':
                pool = [ch for ch in stadium_characters if ch in characters_by_role_full.get(role, [])]
            else:
                pool = characters_by_role_full.get(role, [])
            pool = list(set(pool) - selected_characters)
            character = random.choice(pool) if pool else '???'
            selected_characters.add(character)
            self.character_labels[i].config(text=character)

            img_path = icon_path(character)
            if img_path:
                try:
                    photo = self.photo_cache.get(character)
                    if photo is None:
                        photo = tk.PhotoImage(file=img_path)
                        self.photo_cache[character] = photo
                    self.avatar_labels[i].configure(image=photo)
                    self.avatar_labels[i].image = photo
                    self.avatar_images[i] = photo
                except Exception as error:
                    print(f"Error loading image for {character}: {error}")
                    self.avatar_labels[i].configure(image='', text='')
                    self.avatar_images[i] = None
            else:
                self.avatar_labels[i].configure(image='', text='')
                self.avatar_images[i] = None

    def start_new_game(self, initial=False):
        try:
            count = int(self.game_player_count_var.get())
        except (ValueError, tk.TclError):
            count = 0
        if not 1 <= count <= 10:
            messagebox.showerror('Invalid player count', 'Choose from 1 to 10 players.')
            return
        progressed = any(
            player['points'] != 0 or not self.emergency_available or
            any(value.get().strip() not in ('', '0') for value in player['stats'].values())
            for player in self.game_players
        )
        if not initial and progressed and not messagebox.askyesno(
            'Start new game', 'Reset all points, rosters, and round statistics?'
        ):
            self.game_player_count_var.set(len(self.game_players))
            self.game_pool_var.set(self.active_game_pool)
            return

        names = [player['name'].get() for player in self.game_players]
        self.active_game_pool = self.game_pool_var.get()
        self.game_players = []
        for index in range(count):
            roster = random_game_roster(self.active_game_pool)
            hero = roster[0]
            self.game_players.append({
                'name': tk.StringVar(value=names[index] if index < len(names) else f'Player {index + 1}'),
                'points': 0,
                'heroes': roster,
                'selected': hero,
                'choice': tk.StringVar(value=hero),
                'exact': tk.StringVar(value=hero),
                'target': tk.StringVar(),
                'amount': tk.StringVar(value='50'),
                'stats': {key: tk.StringVar(value='0') for key in STAT_KEYS},
            })
        self.emergency_available = 1
        self.rebuild_game_rows()
        self.refresh_game_ui()
        self.game_status_var.set(f'New {self.active_game_pool} game')

    def player_choices(self):
        return [f"{index + 1}. {player['name'].get().strip() or f'Player {index + 1}'}" for index, player in enumerate(self.game_players)]

    def choice_index(self, value):
        try:
            index = int(value.split('.', 1)[0]) - 1
        except (ValueError, AttributeError):
            return None
        return index if 0 <= index < len(self.game_players) else None

    def rebuild_game_rows(self):
        for widget in self.game_rows_frame.winfo_children():
            widget.destroy()
        self.game_rows.clear()
        hero_names = [hero for _role, hero in all_game_heroes(self.active_game_pool)]
        stat_labels = (('Elim', 'elims'), ('Obj', 'objective'), ('Dmg', 'damage'), ('Heal', 'healing'), ('Death', 'deaths'))

        for index, player in enumerate(self.game_players):
            row = ttk.Frame(self.game_rows_frame, padding=(2, 5))
            row.grid(row=index * 2, column=0, sticky='ew')
            ttk.Label(row, text=f'{index + 1}.', width=3).grid(row=0, column=0, rowspan=2, sticky='w')
            name_entry = ttk.Entry(row, width=16, textvariable=player['name'])
            name_entry.grid(row=0, column=1, padx=(0, 6), sticky='w')
            name_entry.bind('<FocusOut>', lambda _event, i=index: self.on_player_name_change(i))
            name_entry.bind('<Return>', lambda _event, i=index: self.on_player_name_change(i))
            points_label = ttk.Label(row, width=16)
            points_label.grid(row=1, column=1, padx=(0, 6), sticky='w')

            icon_label = tk.Label(row, width=50, height=50)
            icon_label.grid(row=0, column=2, rowspan=2, padx=(0, 8))
            role_label = ttk.Label(row, width=8)
            role_label.grid(row=0, column=3, sticky='w')
            choice_combo = ttk.Combobox(
                row, width=14, state='readonly', textvariable=player['choice'], values=player['heroes']
            )
            choice_combo.grid(row=0, column=4, sticky='w')
            choice_combo.bind('<<ComboboxSelected>>', lambda _event, i=index: self.select_game_hero(i))
            exact_combo = ttk.Combobox(
                row, width=17, state='readonly', textvariable=player['exact'], values=hero_names
            )
            exact_combo.grid(row=0, column=5, padx=(4, 3))
            ttk.Button(row, text='Exact 140', command=lambda i=index: self.buy_exact_hero(i)).grid(
                row=0, column=6, padx=2
            )
            ttk.Button(row, text='Role 85', command=lambda i=index: self.buy_role_reroll(i)).grid(
                row=0, column=7, padx=2
            )
            ttk.Button(row, text='Full 50', command=lambda i=index: self.buy_full_reroll(i)).grid(
                row=0, column=8, padx=2
            )

            stats_frame = ttk.Frame(row)
            stats_frame.grid(row=1, column=3, columnspan=3, pady=(4, 0), sticky='w')
            for column, (label, key) in enumerate(stat_labels):
                ttk.Label(stats_frame, text=f'{label}:').grid(row=0, column=column * 2, padx=(0, 2))
                ttk.Entry(stats_frame, width=6, textvariable=player['stats'][key]).grid(
                    row=0, column=column * 2 + 1, padx=(0, 5)
                )

            transfer_frame = ttk.Frame(row)
            transfer_frame.grid(row=1, column=6, columnspan=3, pady=(4, 0), sticky='w')
            ttk.Label(transfer_frame, text='To:').pack(side='left')
            target_combo = ttk.Combobox(
                transfer_frame, width=13, state='readonly', textvariable=player['target']
            )
            target_combo.pack(side='left', padx=3)
            ttk.Entry(transfer_frame, width=6, textvariable=player['amount']).pack(side='left')
            ttk.Button(transfer_frame, text='Send 2:1', command=lambda i=index: self.transfer_points(i)).pack(
                side='left', padx=(3, 0)
            )

            if index < len(self.game_players) - 1:
                ttk.Separator(self.game_rows_frame).grid(row=index * 2 + 1, column=0, sticky='ew')
            self.game_rows.append({
                'points': points_label,
                'icon': icon_label,
                'role': role_label,
                'choice': choice_combo,
                'exact': exact_combo,
                'target': target_combo,
            })

        self.game_rows_frame.columnconfigure(0, weight=1)
        self.game_rows_frame.update_idletasks()
        self.game_canvas.configure(height=min(520, max(74, len(self.game_players) * 67)))
        self.root.after_idle(self.resize_to_current_tab)

    def refresh_game_ui(self):
        choices = self.player_choices()
        self.emergency_button.config(
            text=f'Emergency all ({self.emergency_available}/1)',
            state='normal' if self.emergency_available else 'disabled',
        )
        for index, (player, widgets) in enumerate(zip(self.game_players, self.game_rows)):
            widgets['points'].config(text=f"Points: {player['points']}")
            widgets['role'].config(text=hero_role(player['selected']))
            widgets['choice']['values'] = player['heroes']
            player['choice'].set(player['selected'])
            widgets['target']['values'] = choices
            target_index = self.choice_index(player['target'].get())
            if target_index is None or target_index == index:
                player['target'].set(choices[(index + 1) % len(choices)])
            photo = self.photo_cache.get(player['selected'])
            if photo is None:
                photo = tk.PhotoImage(file=icon_path(player['selected']))
                self.photo_cache[player['selected']] = photo
            widgets['icon'].configure(image=photo)
            widgets['icon'].image = photo

    def select_game_hero(self, index):
        player = self.game_players[index]
        hero = player['choice'].get()
        if hero in player['heroes']:
            player['selected'] = hero
            self.refresh_game_ui()

    def on_player_name_change(self, index):
        name = self.game_players[index]['name'].get().strip()
        if not name:
            self.game_players[index]['name'].set(f'Player {index + 1}')
        choices = self.player_choices()
        for player_index, (player, row) in enumerate(zip(self.game_players, self.game_rows)):
            target_index = self.choice_index(player['target'].get())
            if target_index is None or target_index == player_index:
                target_index = (player_index + 1) % len(choices)
            player['target'].set(choices[target_index])
            row['target']['values'] = choices

    def buy_exact_hero(self, index):
        player = self.game_players[index]
        hero = player['exact'].get()
        if hero in player['heroes']:
            player['selected'] = hero
            self.refresh_game_ui()
            messagebox.showinfo('Exact hero', 'This hero is already one of the five choices.')
            return
        if player['points'] < 140:
            messagebox.showerror('Not enough points', 'Exact hero costs 140 points.')
            return
        player['points'] -= 140
        slot = player['heroes'].index(player['selected'])
        player['heroes'][slot] = hero
        player['selected'] = hero
        player['choice'].set(hero)
        self.refresh_game_ui()
        self.game_status_var.set(f'{player["name"].get()}: exact hero -140')

    def buy_role_reroll(self, index):
        player = self.game_players[index]
        if player['points'] < 85:
            messagebox.showerror('Not enough points', 'Role random costs 85 points.')
            return
        player['points'] -= 85
        role = hero_role(player['selected'])
        slots = [i for i, hero in enumerate(player['heroes']) if hero_role(hero) == role]
        old = [player['heroes'][i] for i in slots]
        new = old
        for _ in range(10):
            new = random.sample(game_hero_pool(role, self.active_game_pool), len(slots))
            if new != old:
                break
        for slot, hero in zip(slots, new):
            player['heroes'][slot] = hero
        player['selected'] = new[0]
        self.refresh_game_ui()
        self.game_status_var.set(f'{player["name"].get()}: {role} random -85')

    def buy_full_reroll(self, index):
        player = self.game_players[index]
        if player['points'] < 50:
            messagebox.showerror('Not enough points', 'Full random costs 50 points.')
            return
        player['points'] -= 50
        player['heroes'] = random_game_roster(self.active_game_pool)
        player['selected'] = player['heroes'][0]
        self.refresh_game_ui()
        self.game_status_var.set(f'{player["name"].get()}: full random -50')

    def emergency_reroll(self):
        if not self.emergency_available:
            return
        for player in self.game_players:
            player['heroes'] = random_game_roster(self.active_game_pool)
            player['selected'] = player['heroes'][0]
        self.emergency_available = 0
        self.refresh_game_ui()
        self.game_status_var.set('Emergency reroll used for all players')

    def transfer_points(self, source_index):
        source = self.game_players[source_index]
        target_index = self.choice_index(source['target'].get())
        if target_index is None or target_index == source_index:
            messagebox.showerror('Invalid transfer', 'Choose another player.')
            return
        try:
            amount = parse_stat_value(source['amount'].get())
        except ValueError:
            amount = 0
        if amount <= 0:
            messagebox.showerror('Invalid transfer', 'Enter a positive whole number.')
            return
        if source['points'] < amount:
            messagebox.showerror('Not enough points', 'The sender does not have enough points.')
            return
        received = transfer_received(amount)
        source['points'] -= amount
        self.game_players[target_index]['points'] += received
        self.refresh_game_ui()
        self.game_status_var.set(f'Transferred {amount}; received {received}')

    def finish_round(self):
        gains = []
        for player in self.game_players:
            try:
                stats = {
                    key: parse_stat_value(player['stats'][key].get(), objective=(key == 'objective'))
                    for key in STAT_KEYS
                }
            except ValueError:
                messagebox.showerror('Invalid statistics', f"Check the values for {player['name'].get()}.")
                return
            gains.append(calculate_round_points(**stats))

        for player, gain in zip(self.game_players, gains):
            player['points'] += gain
            for value in player['stats'].values():
                value.set('0')
        self.emergency_available = 1
        self.refresh_game_ui()
        self.game_status_var.set('Round complete; emergency rerolls restored')
        messagebox.showinfo(
            'Round complete',
            '\n'.join(f"{player['name'].get()}: {gain:+d}" for player, gain in zip(self.game_players, gains)),
        )

    def load_scoreboard(self):
        if self.ocr_running:
            return
        path = filedialog.askopenfilename(
            title='Scoreboard screenshot',
            filetypes=[('Images', '*.png *.jpg *.jpeg *.bmp *.tif *.tiff'), ('All files', '*.*')],
        )
        if not path:
            return
        self.ocr_running = True
        self.ocr_button.config(state='disabled')
        self.ocr_status_var.set('LiteParse: reading screenshot...')
        names = [player['name'].get() for player in self.game_players]
        threading.Thread(target=self._ocr_worker, args=(path, names), daemon=True).start()

    def _ocr_worker(self, path, names):
        try:
            results, _text = parse_scoreboard_screenshot(path, names)
        except Exception as error:
            self.root.after(0, self._ocr_failed, str(error))
            return
        self.root.after(0, self._ocr_done, results)

    def _ocr_failed(self, error):
        self.ocr_running = False
        self.ocr_button.config(state='normal')
        self.ocr_status_var.set('LiteParse failed; manual input is available')
        messagebox.showerror('LiteParse', error)

    def _ocr_done(self, results):
        self.ocr_running = False
        self.ocr_button.config(state='normal')
        for index, stats in results.items():
            if index >= len(self.game_players):
                continue
            for key, value in stats.items():
                self.game_players[index]['stats'][key].set(str(value))
        matched = len(results)
        self.ocr_status_var.set(
            f'LiteParse: filled {matched}/{len(self.game_players)}; verify before ending the round'
        )
        if not matched:
            messagebox.showwarning('LiteParse', 'No player rows matched. Enter the values manually.')

if __name__ == '__main__':
    if '--check' in sys.argv:
        self_check()
        raise SystemExit

    lower_process_priority()
    root = tk.Tk()
    app = OverwatchRandomizerApp(root)
    root.mainloop()
