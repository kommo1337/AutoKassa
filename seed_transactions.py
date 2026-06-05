#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Скрипт для генерации 150 тестовых транзакций в БД AutoKassa.
Записи за месяц (май 2026) с реалистичными данными автосервиса.
"""

import sqlite3
import random
from datetime import datetime, timedelta
import os

# Путь к БД
DB_PATH = os.path.join(
    os.path.dirname(os.path.abspath(__file__)),
    "AutoKassa", "bin", "Debug", "net10.0-windows", "AutoKassa.db"
)

# Категории
INCOME_CATEGORIES = [
    (1, "Ремонт авто"),
    (2, "Ремонт мото"),
    (3, "Диагностика"),
    (4, "Прочие доходы"),
]

EXPENSE_CATEGORIES = [
    (5, "Запчасти"),
    (6, "Зарплата"),
    (7, "Аренда"),
    (8, "Коммунальные услуги"),
    (9, "Маркетинг"),
    (10, "Прочие расходы"),
]

# Диапазоны сумм по категориям
AMOUNT_RANGES = {
    1: (3000, 50000),    # Ремонт авто
    2: (2000, 25000),    # Ремонт мото
    3: (1000, 8000),     # Диагностика
    4: (500, 15000),     # Прочие доходы
    5: (5000, 40000),    # Запчасти
    6: (30000, 80000),   # Зарплата
    7: (25000, 50000),   # Аренда
    8: (5000, 15000),    # Коммунальные услуги
    9: (3000, 20000),    # Маркетинг
    10: (1000, 10000),   # Прочие расходы
}

# Шаблоны описаний
INCOME_DESCRIPTIONS = {
    1: [
        "Замена масла и фильтров Toyota Camry",
        "Ремонт подвески BMW X5",
        "Замена тормозных колодок передних",
        "Диагностика и ремонт двигателя",
        "Замена ГРМ Volkswagen Polo",
        "Ремонт КПП Hyundai Solaris",
        "Шиномонтаж и балансировка 4 колес",
        "Замена ступичного подшипника",
        "Ремонт электропроводки",
        "Замена амортизаторов задних",
        "Регулировка развала-схождения",
        "Ремонт выхлопной системы",
        "Замена свечей зажигания",
        "Чистка инжектора",
        "Ремонт кондиционера",
    ],
    2: [
        "Замена цепи ГРМ Honda CB400",
        "Ремонт карбюратора Yamaha",
        "Замена масла и фильтра",
        "Шиномонтаж мотоцикла",
        "Регулировка клапанов",
        "Замена тормозных дисков",
        "Ремонт электрики",
        "Установка сигнализации",
    ],
    3: [
        "Компьютерная диагностика двигателя",
        "Диагностика ходовой части",
        "Диагностика ABS и ESP",
        "Проверка состояния АКБ",
        "Диагностика кондиционера",
        "Измерение компрессии",
        "Проверка уровня жидкостей",
        "Диагностика подвески на стенде",
    ],
    4: [
        "Продажа автомасла клиенту",
        "Аренда подъемника",
        "Консультация по ремонту",
        "Продажа аксессуаров",
        "Мойка автомобиля",
        "Химчистка салона",
        "Полировка кузова",
        "Предпродажная подготовка",
    ],
}

EXPENSE_DESCRIPTIONS = {
    5: [
        "Закупка тормозных колодок",
        "Моторное масло Castrol 5W-30",
        "Фильтры масляный, воздушный, салонный",
        "Амортизаторы KYB",
        "Ремень ГРМ Contitech",
        "Подшипники ступичные",
        "Свечи зажигания NGK",
        "Антифриз G12",
        "Тормозная жидкость DOT4",
        "Закупка шин",
        "Аккумулятор 60Ah",
        "Расходники для СТО",
    ],
    6: [
        "Зарплата мастер-приемщику",
        "Зарплата автослесарю",
        "Зарплата диагносту",
        "Зарплата электрику",
        "Зарплата мойщику",
        "Аванс сотрудникам",
        "Премия за выполнение плана",
        "Оплата подработки",
    ],
    7: [
        "Аренда помещения СТО",
        "Аренда склада запчастей",
        "Аренда парковки",
        "Коммунальные платежи за аренду",
    ],
    8: [
        "Электроэнергия",
        "Водоснабжение и канализация",
        "Отопление",
        "Вывоз мусора",
        "Охрана помещения",
        "Интернет и телефон",
    ],
    9: [
        "Реклама в Яндекс Директ",
        "Печать листовок",
        "Размещение на Авито",
        "SMM ведение группы",
        "Баннер у дороги",
        "Скидочные купоны",
        "SEO продвижение сайта",
    ],
    10: [
        "Канцелярские товары",
        "Хозяйственные нужды",
        "Обеды для сотрудников",
        "Бензин для служебного авто",
        "Мелкий ремонт оборудования",
        "Покупка инструментов",
        "Сертификация услуг",
        "Бухгалтерские услуги",
    ],
}


def get_random_amount(category_id: int) -> float:
    """Генерация случайной суммы для категории."""
    low, high = AMOUNT_RANGES[category_id]
    # Округляем до сотен или десятков
    if high > 10000:
        return round(random.randint(low, high) / 100) * 100
    else:
        return round(random.randint(low, high) / 10) * 10


def get_random_description(category_id: int, is_income: bool) -> str:
    """Случайное описание для категории."""
    if is_income:
        return random.choice(INCOME_DESCRIPTIONS.get(category_id, ["Операция"]))
    else:
        return random.choice(EXPENSE_DESCRIPTIONS.get(category_id, ["Операция"]))


def generate_transactions(count: int = 150) -> list:
    """Генерация списка транзакций."""
    transactions = []
    
    # Период: май 2026
    start_date = datetime(2026, 5, 1)
    end_date = datetime(2026, 5, 31)
    days_in_month = (end_date - start_date).days + 1
    
    # Распределение: больше операций в будни, меньше в выходные
    date_weights = []
    for d in range(days_in_month):
        date = start_date + timedelta(days=d)
        if date.weekday() < 5:  # Пн-Пт
            date_weights.append(3)
        else:
            date_weights.append(1)
    
    for i in range(count):
        # Случайная дата с учетом весов
        date_offset = random.choices(range(days_in_month), weights=date_weights, k=1)[0]
        date = start_date + timedelta(days=date_offset)
        
        # Случайное время в рабочие часы 8:00 - 19:00
        hour = random.randint(8, 18)
        minute = random.randint(0, 59)
        second = random.randint(0, 59)
        date = date.replace(hour=hour, minute=minute, second=second)
        
        # Соотношение доход/расход: примерно 55% доход, 45% расход
        is_income = random.random() < 0.55
        
        if is_income:
            category = random.choice(INCOME_CATEGORIES)
        else:
            category = random.choice(EXPENSE_CATEGORIES)
        
        category_id, category_name = category
        amount = get_random_amount(category_id)
        description = get_random_description(category_id, is_income)
        payment_type = 1 if random.random() < 0.6 else 2  # 60% наличные, 40% безнал
        
        # Создание записи
        transaction = {
            "Date": date,
            "Amount": amount,
            "Type": 1 if is_income else 2,
            "PaymentType": payment_type,
            "CategoryId": category_id,
            "Description": description,
            "CreatedAt": date,
            "UpdatedAt": None,
            "IsDeleted": 0,
        }
        transactions.append(transaction)
    
    # Сортируем по дате
    transactions.sort(key=lambda x: x["Date"])
    return transactions


def insert_transactions(transactions: list):
    """Вставка транзакций в БД SQLite."""
    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()
    
    # Проверяем текущее количество
    cursor.execute("SELECT COUNT(*) FROM Transactions WHERE IsDeleted = 0")
    current_count = cursor.fetchone()[0]
    print(f"Текущее количество транзакций в БД: {current_count}")
    
    # Вставка
    sql = """
        INSERT INTO Transactions 
        (Date, Amount, Type, PaymentType, CategoryId, Description, CreatedAt, UpdatedAt, IsDeleted)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
    """
    
    data = [
        (
            t["Date"].isoformat(),
            t["Amount"],
            t["Type"],
            t["PaymentType"],
            t["CategoryId"],
            t["Description"],
            t["CreatedAt"].isoformat(),
            t["UpdatedAt"],
            t["IsDeleted"],
        )
        for t in transactions
    ]
    
    cursor.executemany(sql, data)
    conn.commit()
    
    # Проверяем результат
    cursor.execute("SELECT COUNT(*) FROM Transactions WHERE IsDeleted = 0")
    new_count = cursor.fetchone()[0]
    conn.close()
    
    print(f"Добавлено записей: {len(transactions)}")
    print(f"Всего транзакций в БД: {new_count}")
    
    # Статистика
    income_count = len([t for t in transactions if t["Type"] == 1])
    expense_count = len([t for t in transactions if t["Type"] == 2])
    income_sum = sum(t["Amount"] for t in transactions if t["Type"] == 1)
    expense_sum = sum(t["Amount"] for t in transactions if t["Type"] == 2)
    
    print(f"\nСтатистика добавленных записей:")
    print(f"  Доходы: {income_count} шт. на сумму {income_sum:,.2f} ₽")
    print(f"  Расходы: {expense_count} шт. на сумму {expense_sum:,.2f} ₽")
    print(f"  Баланс: {income_sum - expense_sum:,.2f} ₽")


def main():
    print("=" * 50)
    print("Генерация тестовых данных для AutoKassa")
    print("=" * 50)
    
    if not os.path.exists(DB_PATH):
        print(f"ОШИБКА: БД не найдена по пути {DB_PATH}")
        print("Убедитесь, что приложение хотя бы раз запускалось.")
        return 1
    
    print(f"БД: {DB_PATH}")
    print()
    
    random.seed(42)  # Для воспроизводимости
    transactions = generate_transactions(150)
    insert_transactions(transactions)
    
    print("\nГотово!")
    return 0


if __name__ == "__main__":
    exit(main())
