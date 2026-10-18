
const accountCountElement = document.getElementById("accountCount");
const totalBalanceElement = document.getElementById("totalBalance");
const paymentCountElement = document.getElementById("paymentCount");

const accountsTableBody = document.getElementById("accountsTableBody");
const paymentsTableBody = document.getElementById("paymentsTableBody");

const accountsMessage = document.getElementById("accountsMessage");
const paymentsMessage = document.getElementById("paymentsMessage");
const refreshButton = document.getElementById("refreshButton");

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

async function loadAccounts() {
    accountsMessage.textContent = "Loading accounts...";
    accountsTableBody.replaceChildren();

    try {
        const response = await fetch("/dev/accounts");

        if (!response.ok) {
            throw new Error(`Request failed: ${response.status}`);
        }

        const accounts = await response.json();

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

refreshButton.addEventListener("click", loadDashboard);

loadDashboard();
