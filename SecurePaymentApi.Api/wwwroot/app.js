
const accountCountElement = document.getElementById("accountCount");
const totalBalanceElement = document.getElementById("totalBalance");
const paymentCountElement = document.getElementById("paymentCount");

const accountsTableBody = document.getElementById("accountsTableBody");
const paymentsTableBody = document.getElementById("paymentsTableBody");

const accountsMessage = document.getElementById("accountsMessage");
const paymentsMessage = document.getElementById("paymentsMessage");
const refreshButton = document.getElementById("refreshButton");

const paymentForm = document.getElementById("paymentForm");
const fromAccountSelect = document.getElementById("fromAccount");
const toAccountSelect = document.getElementById("toAccount");
const amountInput = document.getElementById("amount");
const sendPaymentButton = document.getElementById("sendPaymentButton");
const paymentFormMessage = document.getElementById("paymentFormMessage");

let accountsCache = [];
let currentIdempotencyKey = null;

const currencyFormatter = new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR"
});

function formatCurrency(amount) {
    return currencyFormatter.format(amount);
}

function formatDate(dateString) {
    return new Date(dateString).toLocaleString("en-ZA");
}

function shortenId(id) {
    return `${id.slice(0, 8)}...`;
}

function populateAccountSelects(accounts) {
    for (const select of [fromAccountSelect, toAccountSelect]) {
        const previousValue = select.value;

        select.replaceChildren();

        const placeholder = document.createElement("option");
        placeholder.value = "";
        placeholder.textContent = select === fromAccountSelect
            ? "Select sender"
            : "Select recipient";

        select.appendChild(placeholder);

        for (const account of accounts) {
            const option = document.createElement("option");
            option.value = account.id;
            option.textContent =
                `${account.accountHolderId} — ${formatCurrency(account.balance)}`;

            select.appendChild(option);
        }

        if (accounts.some(account => account.id === previousValue)) {
            select.value = previousValue;
        }
    }
}

async function loadAccounts() {
    accountsMessage.textContent = "Loading accounts...";
    accountsTableBody.replaceChildren();

    try {
        const response = await fetch("/dev/accounts");

        if (!response.ok) {
            throw new Error(`Request failed: ${response.status}`);
        }

        const accounts = await response.json();

        accountsCache = accounts;
        populateAccountSelects(accounts);

        accountCountElement.textContent = accounts.length;

        const totalBalance = accounts.reduce(
            (total, account) => total + Number(account.balance),
            0
        );

        totalBalanceElement.textContent = formatCurrency(totalBalance);

        if (accounts.length === 0) {
            accountsMessage.textContent = "No accounts found.";
            return;
        }

        accountsMessage.textContent = "";

        for (const account of accounts) {
            const row = document.createElement("tr");

            const holderCell = document.createElement("td");
            holderCell.textContent = account.accountHolderId;

            const idCell = document.createElement("td");
            idCell.textContent = account.id;

            const balanceCell = document.createElement("td");
            balanceCell.textContent = formatCurrency(account.balance);

            row.append(holderCell, idCell, balanceCell);
            accountsTableBody.appendChild(row);
        }
    } catch (error) {
        accountsMessage.textContent =
            "Could not load accounts. Check that the API is running.";
        console.error("Account loading error:", error);
    }
}

async function loadPayments() {
    paymentsMessage.textContent = "Loading payments...";
    paymentsTableBody.replaceChildren();

    try {
        const response = await fetch("/dev/payments");

        if (!response.ok) {
            throw new Error(`Request failed: ${response.status}`);
        }

        const payments = await response.json();

        paymentCountElement.textContent = payments.length;

        if (payments.length === 0) {
            paymentsMessage.textContent = "No payments found.";
            return;
        }

        paymentsMessage.textContent = "";

        for (const payment of payments) {
            const row = document.createElement("tr");

            const idCell = document.createElement("td");
            idCell.textContent = shortenId(payment.id);

            const fromCell = document.createElement("td");
            fromCell.textContent = shortenId(payment.fromAccountId);

            const toCell = document.createElement("td");
            toCell.textContent = shortenId(payment.toAccountId);

            const amountCell = document.createElement("td");
            amountCell.textContent = formatCurrency(payment.amount);

            const statusCell = document.createElement("td");
            const statusBadge = document.createElement("span");
            statusBadge.className = "status";
            statusBadge.textContent = payment.status;
            statusCell.appendChild(statusBadge);

            const dateCell = document.createElement("td");
            dateCell.textContent = formatDate(payment.createdAtUtc);

            row.append(
                idCell,
                fromCell,
                toCell,
                amountCell,
                statusCell,
                dateCell
            );

            paymentsTableBody.appendChild(row);
        }
    } catch (error) {
        paymentsMessage.textContent =
            "Could not load payments. Check that the API is running.";
        console.error("Payment loading error:", error);
    }
}

async function loadDashboard() {
    refreshButton.disabled = true;
    refreshButton.textContent = "Refreshing...";

    try {
        await Promise.all([
            loadAccounts(),
            loadPayments()
        ]);
    } finally {
        refreshButton.disabled = false;
        refreshButton.textContent = "Refresh data";
    }
}

paymentForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    const fromAccountId = fromAccountSelect.value;
    const toAccountId = toAccountSelect.value;
    const amount = Number(amountInput.value);

    paymentFormMessage.textContent = "";

    if (!fromAccountId || !toAccountId) {
        paymentFormMessage.textContent = "Please select both accounts.";
        return;
    }

    if (fromAccountId === toAccountId) {
        paymentFormMessage.textContent =
            "Sender and recipient must be different accounts.";
        return;
    }

    if (!Number.isFinite(amount) || amount <= 0) {
        paymentFormMessage.textContent =
            "Enter a valid amount greater than zero.";
        return;
    }

    const sender = accountsCache.find(
        account => account.id === fromAccountId
    );

    if (!sender || amount > Number(sender.balance)) {
        paymentFormMessage.textContent =
            "The sender's available balance is insufficient.";
        return;
    }

    // Reuse this key if the request fails and the user retries.
    currentIdempotencyKey ??= crypto.randomUUID();

    sendPaymentButton.disabled = true;
    sendPaymentButton.textContent = "Sending...";

    try {
        const response = await fetch("/payments", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                fromAccountId,
                toAccountId,
                amount,
                idempotencyKey: currentIdempotencyKey
            })
        });

        const result = await response.json().catch(() => ({}));

        if (!response.ok) {
            paymentFormMessage.textContent =
                result.error || `Payment failed (${response.status}).`;
            return;
        }

        paymentFormMessage.textContent =
            `Payment successful: ${formatCurrency(amount)} sent.`;

        paymentForm.reset();
        currentIdempotencyKey = null;

        await loadDashboard();
    } catch (error) {
        paymentFormMessage.textContent =
            "Could not confirm the payment result. Retry using the same key.";
        console.error("Payment submission error:", error);
    } finally {
        sendPaymentButton.disabled = false;
        sendPaymentButton.textContent = "Send payment";
    }
});

refreshButton.addEventListener("click", loadDashboard);

loadDashboard();
